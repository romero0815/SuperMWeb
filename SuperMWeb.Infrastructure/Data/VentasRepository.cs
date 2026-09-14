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


    public async Task<int> RegistrarVentaFiadaAsync(
        int idUsuario,
        string? usuario,
        decimal total,
        IReadOnlyList<LineaTicket> items,
        VentaFiada fiado,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(fiado);
        if (items.Count == 0)
            throw new ArgumentException("La venta no tiene items.", nameof(items));

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

        try
        {
            // 1. La venta se registra igual que cualquier otra: factura, detalle
            //    y salida de inventario. El Cliente queda con el nombre del deudor.
            await using (var cmd = new SqlCommand("RegistrarVenta", con, tx)
                         {
                             CommandType = CommandType.StoredProcedure
                         })
            {
                cmd.Parameters.Add(new SqlParameter("@IdUsuario", SqlDbType.Int) { Value = idUsuario });
                cmd.Parameters.Add(new SqlParameter("@Cliente", SqlDbType.NVarChar, 100)
                {
                    Value = fiado.NombreCliente ?? string.Empty
                });
                cmd.Parameters.Add(new SqlParameter("@Total", SqlDbType.Decimal)
                {
                    Value = total, Precision = 18, Scale = 2
                });

                var tvp = ConstruirTvpDetalleVenta(items);
                var tvpParam = cmd.Parameters.AddWithValue("@ItemsVenta", tvp);
                tvpParam.SqlDbType = SqlDbType.Structured;
                tvpParam.TypeName = "TVP_DetalleVenta";

                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 2. Cliente nuevo: se crea dentro de la misma transaccion.
            var idDeudor = fiado.IdDeudor;
            if (fiado.EsClienteNuevo)
            {
                const string sqlDeudor = @"
                    INSERT INTO Deudor (NombreCliente, Identificacion, Telefono, CupoMaximo, Activo)
                    VALUES (@nombre, @identificacion, @telefono, @cupo, 1);
                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                await using var cmdDeudor = new SqlCommand(sqlDeudor, con, tx);
                cmdDeudor.Parameters.Add(new SqlParameter("@nombre", fiado.NombreCliente));
                cmdDeudor.Parameters.Add(new SqlParameter("@identificacion",
                    string.IsNullOrWhiteSpace(fiado.Identificacion) ? DBNull.Value : fiado.Identificacion.Trim()));
                cmdDeudor.Parameters.Add(new SqlParameter("@telefono",
                    string.IsNullOrWhiteSpace(fiado.Telefono) ? DBNull.Value : fiado.Telefono.Trim()));
                cmdDeudor.Parameters.Add(new SqlParameter("@cupo",
                    fiado.CupoMaximo is null ? DBNull.Value : fiado.CupoMaximo.Value));

                var id = await cmdDeudor.ExecuteScalarAsync(ct);
                idDeudor = id is null || id == DBNull.Value ? 0 : Convert.ToInt32(id);
            }

            // 3. El cargo al deudor, por el valor ajustado en la pantalla de fiado
            //    (puede diferir del total de la venta).
            await DeudoresRepository.InsertarMovimientoAsync(
                con, tx, idDeudor, TipoMovimientoDeudor.Cargo,
                fiado.Valor, fiado.Descripcion, usuario, ct);

            await tx.CommitAsync(ct);
            return idDeudor;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
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