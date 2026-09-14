using SuperMWeb.Domain;

namespace SuperMWeb.Application;

public interface IComprasRepository
{
    // Invoca el stored procedure RegistrarCompra con el TVP_DetalleCompra.
    // El SP inserta la cabecera y el detalle, actualiza Productos.PrecioCompra
    // con el precio pagado en cada linea (comportamiento intencional, R14) y
    // registra el movimiento de Entrada, que es el que sube el stock via
    // trigger sobre MovimientosInventario.
    Task RegistrarCompraAsync(
        int idUsuario,
        int idProveedor,
        decimal total,
        IReadOnlyList<LineaTicket> items,
        CancellationToken ct = default);
}
