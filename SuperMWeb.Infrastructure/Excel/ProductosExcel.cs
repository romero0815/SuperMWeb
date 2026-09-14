using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using SuperMWeb.Application;
using SuperMWeb.Infrastructure.Data;

namespace SuperMWeb.Infrastructure.Excel;

// Carga y descarga masiva de productos. Portado de DataUtil del desktop, con
// dos diferencias deliberadas:
//   1. El desktop insertaba fila por fila y abortaba en el primer error, lo que
//      dejaba media carga aplicada. Aqui se valida el archivo entero primero y
//      se inserta todo dentro de una transaccion: o entra completo o no entra.
//   2. El desktop armaba el INSERT con las columnas que trajera el Excel. Aqui
//      la lista de columnas es fija, para que un encabezado inesperado no se
//      convierta en SQL.
public sealed class ProductosExcel : IProductosExcel
{
    // Mismas cabeceras que Plantillas/CargaProductos.xlsx del desktop.
    private static readonly string[] Cabeceras =
        ["Nombre", "Descripcion", "PrecioCompra", "PrecioVenta", "Active", "Stock", "NombreProveedor"];

    private static readonly string[] Obligatorias =
        ["Nombre", "PrecioCompra", "PrecioVenta", "Stock"];

    private readonly SqlConnectionFactory _factory;

    public ProductosExcel(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public byte[] GenerarPlantilla()
    {
        using var wb = new XLWorkbook();
        var hoja = wb.Worksheets.Add("CargaProductos");

        for (var i = 0; i < Cabeceras.Length; i++)
        {
            var celda = hoja.Cell(1, i + 1);
            celda.Value = Cabeceras[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Fila de ejemplo, igual que la plantilla del desktop.
        hoja.Cell(2, 1).Value = "Producto de ejemplo";
        hoja.Cell(2, 2).Value = "Descripcion opcional";
        hoja.Cell(2, 3).Value = 1000;
        hoja.Cell(2, 4).Value = 1500;
        hoja.Cell(2, 5).Value = "Si";
        hoja.Cell(2, 6).Value = 10;
        hoja.Cell(2, 7).Value = "Ninguno";

        hoja.Columns().AdjustToContents();
        return AGuardar(wb);
    }

    public byte[] Exportar(IReadOnlyList<ProductoListado> productos)
    {
        using var wb = new XLWorkbook();
        var hoja = wb.Worksheets.Add("ProductosActivos");

        string[] columnas = ["IdProducto", "Nombre", "Descripcion", "PrecioCompra", "PrecioVenta", "Stock", "Proveedor"];
        for (var i = 0; i < columnas.Length; i++)
        {
            var celda = hoja.Cell(1, i + 1);
            celda.Value = columnas[i];
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var fila = 2;
        foreach (var p in productos)
        {
            hoja.Cell(fila, 1).Value = p.IdProducto;
            hoja.Cell(fila, 2).Value = p.Nombre;
            hoja.Cell(fila, 3).Value = p.Descripcion ?? string.Empty;
            hoja.Cell(fila, 4).Value = p.PrecioCompra;
            hoja.Cell(fila, 5).Value = p.PrecioVenta;
            hoja.Cell(fila, 6).Value = p.Stock;
            hoja.Cell(fila, 7).Value = p.Proveedor ?? string.Empty;
            fila++;
        }

        hoja.Columns().AdjustToContents();
        return AGuardar(wb);
    }

    public async Task<ResultadoImportacion> ImportarAsync(Stream archivo, CancellationToken ct = default)
    {
        var errores = new List<string>();
        List<FilaProducto> filas;

        try
        {
            filas = LeerFilas(archivo, errores);
        }
        catch (Exception ex)
        {
            return new ResultadoImportacion(0, [$"No se pudo leer el archivo: {ex.Message}"]);
        }

        if (errores.Count > 0) return new ResultadoImportacion(0, errores);
        if (filas.Count == 0) return new ResultadoImportacion(0, ["El archivo no contiene datos."]);

        // Resolver los nombres de proveedor contra la base antes de insertar.
        var proveedores = await CargarProveedoresAsync(ct);
        foreach (var f in filas)
        {
            if (string.IsNullOrWhiteSpace(f.NombreProveedor)) continue;

            if (proveedores.TryGetValue(f.NombreProveedor.Trim(), out var idProv))
                f.IdProveedor = idProv;
            else
                errores.Add($"Fila {f.Numero}: el proveedor '{f.NombreProveedor}' no existe.");
        }

        if (errores.Count > 0) return new ResultadoImportacion(0, errores);

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

        const string sql = @"
            INSERT INTO Productos
                (Nombre, Descripcion, PrecioCompra, PrecioVenta, Stock, IdProveedor, Active, CodigoBarras)
            VALUES
                (@nombre, @descripcion, @precioC, @precioV, @stock, @idProveedor, @active, NULL)";

        try
        {
            foreach (var f in filas)
            {
                await using var cmd = new SqlCommand(sql, con, tx);
                cmd.Parameters.Add(new SqlParameter("@nombre", f.Nombre));
                cmd.Parameters.Add(new SqlParameter("@descripcion",
                    string.IsNullOrWhiteSpace(f.Descripcion) ? DBNull.Value : f.Descripcion));
                cmd.Parameters.Add(new SqlParameter("@precioC", f.PrecioCompra));
                cmd.Parameters.Add(new SqlParameter("@precioV", f.PrecioVenta));
                cmd.Parameters.Add(new SqlParameter("@stock", f.Stock));
                cmd.Parameters.Add(new SqlParameter("@idProveedor", (object?)f.IdProveedor ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@active", f.Active ? 1 : 0));
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return new ResultadoImportacion(filas.Count, []);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new ResultadoImportacion(0, [$"Error insertando: {ex.Message}. No se cargo ninguna fila."]);
        }
    }

    // ---------- Lectura y validacion ----------

    private static List<FilaProducto> LeerFilas(Stream archivo, List<string> errores)
    {
        using var wb = new XLWorkbook(archivo);
        var hoja = wb.Worksheet(1);
        var filasUsadas = hoja.RowsUsed().ToList();

        if (filasUsadas.Count == 0)
        {
            errores.Add("El archivo esta vacio.");
            return [];
        }

        // Mapa cabecera -> numero de columna.
        var encabezado = filasUsadas[0];
        var columnas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var celda in encabezado.CellsUsed())
        {
            var nombre = celda.Value.ToString().Trim();
            if (nombre.Length > 0) columnas[nombre] = celda.Address.ColumnNumber;
        }

        var faltantes = Obligatorias.Where(c => !columnas.ContainsKey(c)).ToList();
        if (faltantes.Count > 0)
        {
            errores.Add($"Faltan columnas obligatorias: {string.Join(", ", faltantes)}.");
            return [];
        }

        var resultado = new List<FilaProducto>();

        for (var i = 1; i < filasUsadas.Count; i++)
        {
            var fila = filasUsadas[i];
            var numero = i; // numero de registro, igual que el desktop (1 = primera fila de datos)

            string Texto(string columna) =>
                columnas.TryGetValue(columna, out var c)
                    ? fila.Cell(c).Value.ToString().Trim()
                    : string.Empty;

            var nombre = Texto("Nombre");
            var precioC = Texto("PrecioCompra");
            var precioV = Texto("PrecioVenta");
            var stock = Texto("Stock");

            // Fila completamente vacia: se ignora en silencio.
            if (nombre.Length == 0 && precioC.Length == 0 && precioV.Length == 0 && stock.Length == 0)
                continue;

            var faltaObligatorio = false;
            foreach (var (columna, valor) in new[]
                     { ("Nombre", nombre), ("PrecioCompra", precioC), ("PrecioVenta", precioV), ("Stock", stock) })
            {
                if (valor.Length == 0)
                {
                    errores.Add($"Fila {numero}: el campo '{columna}' es obligatorio.");
                    faltaObligatorio = true;
                }
            }
            if (faltaObligatorio) continue;

            if (!FormatosTexto.IsNumberDecimal(precioC))
            {
                errores.Add($"Fila {numero}: 'PrecioCompra' debe ser un numero decimal.");
                continue;
            }
            if (!FormatosTexto.IsNumberDecimal(precioV))
            {
                errores.Add($"Fila {numero}: 'PrecioVenta' debe ser un numero decimal.");
                continue;
            }
            if (!FormatosTexto.IsNumber(stock))
            {
                errores.Add($"Fila {numero}: 'Stock' debe ser un numero entero.");
                continue;
            }

            var activeTexto = Texto("Active").ToLowerInvariant();
            // El desktop trata "si"/"sí"/"1"/"true" como activo y todo lo demas
            // como inactivo. Si la columna no viene, el producto entra activo.
            var active = !columnas.ContainsKey("Active")
                         || activeTexto is "si" or "sí" or "1" or "true";

            resultado.Add(new FilaProducto
            {
                Numero = numero,
                Nombre = nombre,
                Descripcion = Texto("Descripcion"),
                PrecioCompra = FormatosTexto.ParseDecimal(precioC),
                PrecioVenta = FormatosTexto.ParseDecimal(precioV),
                Stock = int.Parse(stock),
                Active = active,
                NombreProveedor = Texto("NombreProveedor")
            });
        }

        return resultado;
    }

    private async Task<Dictionary<string, int>> CargarProveedoresAsync(CancellationToken ct)
    {
        const string sql = "SELECT IdProveedor, Nombre FROM Proveedores";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(ct)) mapa[reader.GetString(1)] = reader.GetInt32(0);
        return mapa;
    }

    private static byte[] AGuardar(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private sealed class FilaProducto
    {
        public int Numero { get; init; }
        public string Nombre { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public decimal PrecioCompra { get; init; }
        public decimal PrecioVenta { get; init; }
        public int Stock { get; init; }
        public bool Active { get; init; }
        public string NombreProveedor { get; init; } = string.Empty;
        public int? IdProveedor { get; set; }
    }
}
