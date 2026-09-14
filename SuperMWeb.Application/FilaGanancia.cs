namespace SuperMWeb.Application;

// Fila del reporte de ganancias (ReporteGananciasForm).
// OJO: PrecioCompra y TotalCostos usan el PrecioCompra ACTUAL del producto,
// no el historico de cada venta. Es intencional y confirmado por el usuario
// (ver R8/R14 del inventario): "PrecioCompra" significa "ultimo precio pagado".
public record FilaGanancia(
    string Producto,
    int CantidadVendida,
    decimal PrecioVentaProducto,
    decimal TotalVentas,
    decimal PrecioCompraProducto,
    decimal TotalCostos,
    decimal Ganancia);
