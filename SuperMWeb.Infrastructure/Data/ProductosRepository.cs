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
