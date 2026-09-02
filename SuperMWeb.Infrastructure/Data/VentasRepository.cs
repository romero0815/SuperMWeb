using System.Data;
using Microsoft.Data.SqlClient;
using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Infrastructure.Data;

public sealed class VentasRepository : IVentasRepository
{
    private readonly SqlConnectionFactory _factory;

    public VentasRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task RegistrarVentaAsync(
        int idUsuario,
        string cliente,
        decimal total,
        IReadOnlyList<LineaTicket> items,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(items);

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand("RegistrarVenta", con)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.Add(new SqlParameter("@IdUsuario", SqlDbType.Int) { Value = idUsuario });
        cmd.Parameters.Add(new SqlParameter("@Cliente", SqlDbType.NVarChar, 100) { Value = cliente });
        cmd.Parameters.Add(new SqlParameter("@Total", SqlDbType.Decimal) { Value = total, Precision = 18, Scale = 2 });

        var tvp = ConstruirTvpDetalleVenta(items);
        var tvpParam = cmd.Parameters.AddWithValue("@ItemsVenta", tvp);
        tvpParam.SqlDbType = SqlDbType.Structured;
        tvpParam.TypeName = "TVP_DetalleVenta";

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static DataTable ConstruirTvpDetalleVenta(IEnumerable<LineaTicket> items)
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
