namespace SuperMWeb.Application;

// Tarjetas de inventario del dashboard (ReportesForm.CargarInventarioGeneral).
// Solo cuenta productos con Active = 1.
public record ResumenInventario(
    int TotalProductos,
    int TotalUnidades,
    decimal ValorTotalCompra,
    decimal ValorTotalVenta);
