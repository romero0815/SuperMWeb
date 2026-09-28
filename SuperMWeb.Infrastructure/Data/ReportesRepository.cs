using Microsoft.Data.SqlClient;
using SuperMWeb.Application;

namespace SuperMWeb.Infrastructure.Data;

// Consultas analiticas portadas del desktop. A diferencia del desktop, que
// interpolaba las fechas en el string SQL (R3 del inventario), aqui todo va
// parametrizado.
public sealed class ReportesRepository : IReportesRepository
{
    private readonly SqlConnectionFactory _factory;

    public ReportesRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    // ---------- Dashboard ----------

    public async Task<ResumenInventario> ObtenerResumenInventarioAsync(CancellationToken ct = default)
    {
        // ISNULL sobre los SUM: con la tabla vacia devuelven NULL y el
        // desktop reventaba en GetInt32/GetDecimal.
        const string sql = @"
            SELECT COUNT(*)                              AS TotalProductos,
                   ISNULL(SUM(Stock), 0)                 AS TotalUnidades,
                   ISNULL(SUM(Stock * PrecioCompra), 0)  AS ValorTotalCompra,
                   ISNULL(SUM(Stock * PrecioVenta), 0)   AS ValorTotalVenta
            FROM Productos
            WHERE Active = 1";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct)) return new ResumenInventario(0, 0, 0m, 0m);

        return new ResumenInventario(
            TotalProductos: reader.GetInt32(0),
            TotalUnidades: reader.GetInt32(1),
            ValorTotalCompra: reader.GetDecimal(2),
            ValorTotalVenta: reader.GetDecimal(3));
    }

    public Task<IReadOnlyList<ProductoMasVendido>> ListarMasVendidosDelMesAsync(CancellationToken ct = default)
    {
        const string sql = @"
                    SELECT TOP(20) CONCAT(p.Nombre, ' ', p.Descripcion) AS Producto, SUM(df.Cantidad) AS TotalVendido
        FROM DetalleFacturas df
        INNER JOIN Productos p ON df.IdProducto = p.IdProducto
        INNER JOIN Facturas f  ON df.IdFactura  = f.IdFactura
        WHERE MONTH(f.Fecha) = MONTH(GETDATE())
          AND YEAR(f.Fecha)  = YEAR(GETDATE())
        GROUP BY p.Nombre, p.Descripcion
        ORDER BY TotalVendido DESC";

        return LeerListaAsync(sql, null, ct,
            r => new ProductoMasVendido(r.GetString(0), r.GetInt32(1)));
    }

    public Task<IReadOnlyList<ProductoStockBajo>> ListarStockBajoAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP(150) ISNULL(p.CodigoBarras, '') AS CodigoBarras, 
                   CONCAT(p.Nombre, ' ', p.descripcion) AS Nombre, 
                   p.Stock
            FROM Productos p
            WHERE p.Stock <= 5 AND p.Active = 1
            AND EXISTS(
                SELECT 1 FROM DetalleFacturas df
                INNER JOIN Facturas f ON df.IdFactura = f.IdFactura 
                WHERE df.IdProducto = p.IdProducto
                AND f.Fecha >= DATEADD(MONTH, -3, GETDATE())
            )
            ORDER BY p.Stock ASC";

        return LeerListaAsync(sql, null, ct,
            r => new ProductoStockBajo(r.IsDBNull(0) ? string.Empty : r.GetString(0), r.GetString(1), r.GetInt32(2),string.Empty));
    }

    public Task<IReadOnlyList<ProductoStockBajo>> ListarStockBajoHistoricoAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 
                p.CodigoBarras,
                p.Nombre, 
                p.Stock,
                MAX(f.Fecha) AS UltimaVenta
            FROM Productos p
            LEFT JOIN DetalleFacturas df ON df.IdProducto = p.IdProducto
            LEFT JOIN Facturas f ON df.IdFactura = f.IdFactura
            WHERE p.Active = 1
            GROUP BY p.IdProducto, p.Nombre, p.Stock, p.CodigoBarras
            HAVING (MAX(f.Fecha) < DATEADD(MONTH, -3, GETDATE()) and MAX(f.Fecha) >= DATEADD(MONTH, -12, GETDATE()))
                OR MAX(f.Fecha) IS NULL
            ORDER BY UltimaVenta ASC";

        return LeerListaAsync(sql, null, ct,
            r => new ProductoStockBajo(r.IsDBNull(0) ? string.Empty : r.GetString(0), r.GetString(1), r.GetInt32(2),
            r.IsDBNull(3) ? "Sin Venta" : r.GetDateTime(3).ToString("dd-MM-yyyy")));
    }

    // ---------- Ventas ----------

    private const string SqlVentasBase = @"
        SELECT v.IdFactura, v.Fecha, v.Total, c.Nombre AS Cajero
        FROM Facturas v
        LEFT JOIN Usuarios c ON v.IdUsuario = c.IdUsuario";

    public Task<IReadOnlyList<ResumenVenta>> ListarVentasDelDiaAsync(CancellationToken ct = default) =>
        LeerListaAsync(
            SqlVentasBase + @"
            WHERE CAST(v.Fecha AS DATE) = CAST(GETDATE() AS DATE)
            ORDER BY v.Fecha DESC",
            null, ct, LeerResumenVenta);

    public Task<IReadOnlyList<ResumenVenta>> ListarVentasEntreAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default) =>
        LeerListaAsync(
            SqlVentasBase + @"
            WHERE v.Fecha >= @desde AND v.Fecha < @hasta
            ORDER BY v.Fecha DESC",
            cmd => AgregarRango(cmd, desde, hasta), ct, LeerResumenVenta);

    private static ResumenVenta LeerResumenVenta(SqlDataReader r) => new(
        IdFactura: r.GetInt32(0),
        Fecha: r.GetDateTime(1),
        Total: r.GetDecimal(2),
        Cajero: r.IsDBNull(3) ? null : r.GetString(3));

    // ---------- Compras ----------

    private const string SqlComprasBase = @"
        SELECT v.IdCompra, v.Fecha, v.Total, c.Nombre AS Proveedor
        FROM Compras v
        LEFT JOIN Proveedores c ON v.IdProveedor = c.IdProveedor";

    public Task<IReadOnlyList<ResumenCompra>> ListarComprasDelDiaAsync(CancellationToken ct = default) =>
        LeerListaAsync(
            SqlComprasBase + @"
            WHERE CAST(v.Fecha AS DATE) = CAST(GETDATE() AS DATE)
            ORDER BY v.Fecha DESC",
            null, ct, LeerResumenCompra);

    public Task<IReadOnlyList<ResumenCompra>> ListarComprasEntreAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default) =>
        LeerListaAsync(
            SqlComprasBase + @"
            WHERE v.Fecha >= @desde AND v.Fecha < @hasta
            ORDER BY v.Fecha DESC",
            cmd => AgregarRango(cmd, desde, hasta), ct, LeerResumenCompra);

    private static ResumenCompra LeerResumenCompra(SqlDataReader r) => new(
        IdCompra: r.GetInt32(0),
        Fecha: r.GetDateTime(1),
        Total: r.GetDecimal(2),
        Proveedor: r.IsDBNull(3) ? null : r.GetString(3));

    // ---------- Ganancias ----------

    public Task<IReadOnlyList<FilaGanancia>> ListarGananciasEntreAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        // El desktop usaba BETWEEN con el limite superior ya desplazado un dia;
        // aqui se usa >= / < que cubre el mismo rango sin el borde inclusivo.
        const string sql = @"
            SELECT p.Nombre                                              AS Producto,
                   SUM(fd.Cantidad)                                      AS CantidadVendida,
                   p.PrecioVenta                                         AS PrecioVentaProducto,
                   SUM(fd.Cantidad * fd.PrecioUnitario)                  AS TotalVentas,
                   p.PrecioCompra                                        AS PrecioCompraProducto,
                   SUM(fd.Cantidad * p.PrecioCompra)                     AS TotalCostos,
                   SUM((fd.PrecioUnitario - p.PrecioCompra) * fd.Cantidad) AS Ganancia
            FROM DetalleFacturas fd
            INNER JOIN Productos p ON fd.IdProducto = p.IdProducto
            INNER JOIN Facturas  f ON fd.IdFactura  = f.IdFactura
            WHERE f.Fecha >= @desde AND f.Fecha < @hasta
            GROUP BY p.Nombre, p.PrecioVenta, p.PrecioCompra
            ORDER BY Ganancia DESC";

        return LeerListaAsync(sql, cmd => AgregarRango(cmd, desde, hasta), ct,
            r => new FilaGanancia(
                Producto: r.GetString(0),
                CantidadVendida: r.GetInt32(1),
                PrecioVentaProducto: r.GetDecimal(2),
                TotalVentas: r.GetDecimal(3),
                PrecioCompraProducto: r.GetDecimal(4),
                TotalCostos: r.GetDecimal(5),
                Ganancia: r.GetDecimal(6)));
    }

    // ---------- Detalle de documento ----------

    public Task<IReadOnlyList<LineaDocumento>> ListarDetalleDocumentoAsync(
        int id, bool esCompra, CancellationToken ct = default)
    {
        var sql = esCompra
            ? @"SELECT p.IdProducto, v.Fecha, p.Nombre, p.Descripcion, p.PrecioVenta,
                       dv.Cantidad, dv.Subtotal, m.TipoMovimiento, m.Observaciones
                FROM Compras v
                LEFT JOIN DetalleCompras dv ON v.IdCompra = dv.IdCompra
                LEFT JOIN Productos p       ON dv.IdProducto = p.IdProducto
                LEFT JOIN MovimientosInventario m
                       ON v.IdCompra = m.IdReferencia AND dv.IdProducto = m.IdProducto
                WHERE v.IdCompra = @id"
            : @"SELECT p.IdProducto, v.Fecha, p.Nombre, p.Descripcion, p.PrecioVenta,
                       dv.Cantidad, dv.Subtotal, m.TipoMovimiento, m.Observaciones
                FROM Facturas v
                LEFT JOIN DetalleFacturas dv ON v.IdFactura = dv.IdFactura
                LEFT JOIN Productos p        ON dv.IdProducto = p.IdProducto
                LEFT JOIN MovimientosInventario m
                       ON v.IdFactura = m.IdReferencia AND dv.IdProducto = m.IdProducto
                WHERE v.IdFactura = @id";

        return LeerListaAsync(sql,
            cmd => cmd.Parameters.Add(new SqlParameter("@id", id)),
            ct,
            r => new LineaDocumento(
                IdProducto: r.IsDBNull(0) ? 0 : r.GetInt32(0),
                Fecha: r.GetDateTime(1),
                NombreProducto: r.IsDBNull(2) ? string.Empty : r.GetString(2),
                DescripcionProducto: r.IsDBNull(3) ? null : r.GetString(3),
                Precio: r.IsDBNull(4) ? 0m : r.GetDecimal(4),
                Cantidad: r.IsDBNull(5) ? 0 : r.GetInt32(5),
                Subtotal: r.IsDBNull(6) ? 0m : r.GetDecimal(6),
                TipoMovimiento: r.IsDBNull(7) ? null : r.GetString(7),
                Observaciones: r.IsDBNull(8) ? null : r.GetString(8)));
    }

    // ---------- Helpers ----------

    private static void AgregarRango(SqlCommand cmd, DateTime desde, DateTime hasta)
    {
        cmd.Parameters.Add(new SqlParameter("@desde", System.Data.SqlDbType.DateTime) { Value = desde });
        cmd.Parameters.Add(new SqlParameter("@hasta", System.Data.SqlDbType.DateTime) { Value = hasta });
    }

    private async Task<IReadOnlyList<T>> LeerListaAsync<T>(
        string sql,
        Action<SqlCommand>? parametros,
        CancellationToken ct,
        Func<SqlDataReader, T> mapear)
    {
        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        parametros?.Invoke(cmd);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<T>();
        while (await reader.ReadAsync(ct)) lista.Add(mapear(reader));
        return lista;
    }
}
