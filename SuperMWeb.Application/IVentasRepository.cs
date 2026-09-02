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
}
