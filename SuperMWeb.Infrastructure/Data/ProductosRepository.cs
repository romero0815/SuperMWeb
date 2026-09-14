using Microsoft.Data.SqlClient;
using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Infrastructure.Data;

public sealed class ProductosRepository : IProductosRepository
{
    private readonly SqlConnectionFactory _factory;

    public ProductosRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Producto?> BuscarPorCodigoBarrasAsync(string codigo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        const string sql = @"
            SELECT p.IdProducto, p.Nombre, p.Descripcion, p.PrecioCompra, p.PrecioVenta,
                   p.Stock, p.IdProveedor, p.Active, p.CodigoBarras
            FROM Productos p
            WHERE p.Active = 1 AND p.CodigoBarras = @codigo";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@codigo", codigo));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return LeerProducto(reader);
    }

    public async Task<IReadOnlyList<Producto>> ListarDisponiblesParaVentaAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP(250) p.IdProducto, p.Nombre, p.Descripcion, p.PrecioCompra, p.PrecioVenta,
                            p.Stock, p.IdProveedor, p.Active, p.CodigoBarras
            FROM Productos p
            WHERE p.Active = 1 AND p.Stock <> 0";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var lista = new List<Producto>();
        while (await reader.ReadAsync(ct)) lista.Add(LeerProducto(reader));
        return lista;
    }

    public async Task<int> ObtenerStockAsync(int idProducto, CancellationToken ct = default)
    {
        const string sql = "SELECT Stock FROM Productos WHERE IdProducto = @id";
        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idProducto));

        var res = await cmd.ExecuteScalarAsync(ct);
        return res == null || res == DBNull.Value ? 0 : Convert.ToInt32(res);
    }


    // ---------- Gestion de productos ----------

    private const string SqlListadoBase = @"
        SELECT p.IdProducto, p.Nombre, p.Descripcion, p.PrecioCompra, p.PrecioVenta,
               p.Stock, pro.Nombre AS Proveedor
        FROM Productos p
        LEFT JOIN Proveedores pro ON p.IdProveedor = pro.IdProveedor
        WHERE p.Active = 1";

    public async Task<IReadOnlyList<ProductoListado>> ListarAsync(
        string? filtroNombre = null,
        string? codigoBarras = null,
        int? limite = 250,
        int? idProveedor = null,
        bool soloConStock = false,
        CancellationToken ct = default)
    {
        // El TOP solo aplica al listado sin filtro, igual que el desktop:
        // buscar por nombre o por codigo devolvia todas las coincidencias.
        var seleccion = limite is > 0
            ? $"SELECT TOP({limite.Value}) p.IdProducto"
            : "SELECT p.IdProducto";
        var baseSql = SqlListadoBase.Replace("SELECT p.IdProducto", seleccion);

        if (idProveedor is not null) baseSql += " AND p.IdProveedor = @idProveedor";
        if (soloConStock) baseSql += " AND p.Stock <> 0";

        string sql;
        SqlParameter? parametro = null;

        if (!string.IsNullOrWhiteSpace(codigoBarras))
        {
            sql = baseSql + " AND p.CodigoBarras = @codigo ORDER BY p.Nombre";
            parametro = new SqlParameter("@codigo", codigoBarras.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(filtroNombre))
        {
            // El desktop concatenaba el texto en el SQL (R3) y un nombre con
            // comilla simple rompia la consulta. Aqui va parametrizado y los
            // comodines de LIKE se escapan.
            sql = baseSql + " AND p.Nombre LIKE @nombre ESCAPE '\\' ORDER BY p.Nombre";
            parametro = new SqlParameter("@nombre", "%" + EscaparLike(filtroNombre.Trim()) + "%");
        }
        else
        {
            sql = baseSql + " ORDER BY p.Nombre";
        }

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        if (parametro is not null) cmd.Parameters.Add(parametro);
        if (idProveedor is not null) cmd.Parameters.Add(new SqlParameter("@idProveedor", idProveedor.Value));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<ProductoListado>();
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new ProductoListado(
                IdProducto: reader.GetInt32(0),
                Nombre: reader.GetString(1),
                Descripcion: reader.IsDBNull(2) ? null : reader.GetString(2),
                PrecioCompra: reader.GetDecimal(3),
                PrecioVenta: reader.GetDecimal(4),
                Stock: reader.GetInt32(5),
                Proveedor: reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return lista;
    }

    public async Task<Producto?> ObtenerPorIdAsync(int idProducto, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT p.IdProducto, p.Nombre, p.Descripcion, p.PrecioCompra, p.PrecioVenta,
                   p.Stock, p.IdProveedor, p.Active, p.CodigoBarras
            FROM Productos p
            WHERE p.IdProducto = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idProducto));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return LeerProducto(reader);
    }

    public async Task<int> CrearAsync(Producto producto, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO Productos
                (Nombre, Descripcion, PrecioCompra, PrecioVenta, Stock, IdProveedor, Active, CodigoBarras)
            VALUES
                (@nombre, @descripcion, @precioC, @precioV, @stock, @idProveedor, 1, @barcod);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        AgregarCampos(cmd, producto);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id is null || id == DBNull.Value ? 0 : Convert.ToInt32(id);
    }

    public async Task ActualizarAsync(Producto producto, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE Productos
            SET Nombre = @nombre, Descripcion = @descripcion, PrecioCompra = @precioC,
                PrecioVenta = @precioV, Stock = @stock, IdProveedor = @idProveedor,
                CodigoBarras = @barcod
            WHERE IdProducto = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        AgregarCampos(cmd, producto);
        cmd.Parameters.Add(new SqlParameter("@id", producto.IdProducto));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DesactivarAsync(int idProducto, CancellationToken ct = default)
    {
        const string sql = "UPDATE Productos SET Active = 0 WHERE IdProducto = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idProducto));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> ExisteCodigoBarrasAsync(
        string codigoBarras, int? idExcluir = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigoBarras)) return false;

        var sql = @"SELECT COUNT(*) FROM Productos
                    WHERE CodigoBarras = @codigo AND Active = 1";
        if (idExcluir is not null) sql += " AND IdProducto <> @excluir";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@codigo", codigoBarras.Trim()));
        if (idExcluir is not null) cmd.Parameters.Add(new SqlParameter("@excluir", idExcluir.Value));

        var res = await cmd.ExecuteScalarAsync(ct);
        return res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0;
    }

    public async Task<bool> EliminarAsync(int idProducto, CancellationToken ct = default)
    {
        // El desktop hacia dos COUNT por separado y sumaba. Aqui se resuelve
        // en una consulta: si el producto tiene movimientos no se borra.
        const string sqlTieneMovimientos = @"
            SELECT CASE WHEN EXISTS (SELECT 1 FROM DetalleCompras  WHERE IdProducto = @id)
                          OR EXISTS (SELECT 1 FROM DetalleFacturas WHERE IdProducto = @id)
                        THEN 1 ELSE 0 END";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);

        bool tieneMovimientos;
        await using (var cmd = new SqlCommand(sqlTieneMovimientos, con))
        {
            cmd.Parameters.Add(new SqlParameter("@id", idProducto));
            var res = await cmd.ExecuteScalarAsync(ct);
            tieneMovimientos = res is not null && res != DBNull.Value && Convert.ToInt32(res) == 1;
        }

        var sql = tieneMovimientos
            ? "UPDATE Productos SET Active = 0 WHERE IdProducto = @id"
            : "DELETE FROM Productos WHERE IdProducto = @id";

        await using (var cmd = new SqlCommand(sql, con))
        {
            cmd.Parameters.Add(new SqlParameter("@id", idProducto));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return !tieneMovimientos;
    }

    private static void AgregarCampos(SqlCommand cmd, Producto p)
    {
        cmd.Parameters.Add(new SqlParameter("@nombre", p.Nombre));
        cmd.Parameters.Add(new SqlParameter("@descripcion", (object?)p.Descripcion ?? DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@precioC", p.PrecioCompra));
        cmd.Parameters.Add(new SqlParameter("@precioV", p.PrecioVenta));
        cmd.Parameters.Add(new SqlParameter("@stock", p.Stock));
        cmd.Parameters.Add(new SqlParameter("@idProveedor", (object?)p.IdProveedor ?? DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@barcod", (object?)p.CodigoBarras ?? DBNull.Value));
    }

    // Neutraliza los comodines de LIKE para que un nombre con % o _ se busque literal.
    private static string EscaparLike(string valor) => valor
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_")
        .Replace("[", "\\[");

    private static Producto LeerProducto(SqlDataReader r) => new()
    {
        IdProducto = r.GetInt32(0),
        Nombre = r.GetString(1),
        Descripcion = r.IsDBNull(2) ? null : r.GetString(2),
        PrecioCompra = r.GetDecimal(3),
        PrecioVenta = r.GetDecimal(4),
        Stock = r.GetInt32(5),
        IdProveedor = r.IsDBNull(6) ? null : r.GetInt32(6),
        Active = r.IsDBNull(7) ? null : r.GetBoolean(7),
        CodigoBarras = r.IsDBNull(8) ? null : r.GetString(8),
    };
}
