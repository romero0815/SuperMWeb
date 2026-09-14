namespace SuperMWeb.Application;

// Cabecera de compra para los reportes diario y por rango
// (ReporteDiarioComprasForm / ReporteMensualCompraForm).
public record ResumenCompra(int IdCompra, DateTime Fecha, decimal Total, string? Proveedor);
