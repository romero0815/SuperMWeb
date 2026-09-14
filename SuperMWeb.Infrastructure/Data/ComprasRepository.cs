using System.Data;
using Microsoft.Data.SqlClient;
using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Infrastructure.Data;

// Portado de ComprasForm.btnRegistrar_Click del desktop.
public sealed class ComprasRepository : IComprasRepository
{
    private readonly SqlConnectionFactory _factory;

    public ComprasRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task RegistrarCompraAsync(
        int idUsuario,
        int idProveedor,
        decimal total,
        IReadOnlyList<LineaTicket> items,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new ArgumentException("La compra no tiene items.", nameof(items));

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("RegistrarCompra", con)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.Add(new SqlParameter("@IdUsuario", SqlDbType.Int) { Value = idUsuario });
        cmd.Parameters.Add(new SqlParameter("@IdProveedor", SqlDbType.Int) { Value = idProveedor });
        cmd.Parameters.Add(new SqlParameter("@Total", SqlDbType.Decimal) { Value = total, Precision = 18, Scale = 2 });

        var tvp = ConstruirTvpDetalleCompra(items);
        var tvpParam = cmd.Parameters.AddWithValue("@ItemsCompra", tvp);
        tvpParam.SqlDbType = SqlDbType.Structured;
        tvpParam.TypeName = "TVP_DetalleCompra";

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static DataTable ConstruirTvpDetalleCompra(IEnumerable<LineaTicket> items)
    {
        var t = new DataTable();
        t.Columns.Add("IdProducto", typeof(int));
        t.Columns.Add("Cantidad", typeof(int));
        t.Columns.Add("PrecioUnitario", typeof(decimal));
        foreach (var i in items)
        {
            t.Rows.Add(i.IdProducto, i.Cantidad, i.PrecioUnitario);
        }
        return t;
    }
}
