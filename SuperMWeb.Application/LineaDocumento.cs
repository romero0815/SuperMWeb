namespace SuperMWeb.Application;

// Linea del detalle de una factura de venta o de una compra
// (DetalleFacturaForm). TipoMovimiento y Observaciones vienen del LEFT JOIN
// a MovimientosInventario y pueden ser null si el movimiento no existe.
public record LineaDocumento(
    int IdProducto,
    DateTime Fecha,
    string NombreProducto,
    string? DescripcionProducto,
    decimal Precio,
    int Cantidad,
    decimal Subtotal,
    string? TipoMovimiento,
    string? Observaciones);
