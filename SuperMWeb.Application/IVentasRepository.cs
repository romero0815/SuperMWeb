using SuperMWeb.Domain;

namespace SuperMWeb.Application;

public interface IVentasRepository
{
    // Invoca el stored procedure RegistrarVenta con el TVP_DetalleVenta.
    // El total lo calcula el llamador (usar VentaCalculadora.CalcularTotal).
    // Los items deberian venir ya agrupados (usar VentaCalculadora.AgruparDetalle).
    // El SP descuenta stock via trigger en MovimientosInventario.
    Task RegistrarVentaAsync(
        int idUsuario,
        string cliente,
        decimal total,
        IReadOnlyList<LineaTicket> items,
        CancellationToken ct = default);

    // Venta fiada: registra la factura con el SP RegistrarVenta, crea el
    // deudor si es cliente nuevo y le carga la deuda, todo dentro de una
    // misma transaccion. Si algo falla no queda ni la venta ni el cargo.
    // Devuelve el IdDeudor afectado.
    Task<int> RegistrarVentaFiadaAsync(
        int idUsuario,
        string? usuario,
        decimal total,
        IReadOnlyList<LineaTicket> items,
        VentaFiada fiado,
        CancellationToken ct = default);
}
