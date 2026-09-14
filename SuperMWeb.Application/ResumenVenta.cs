namespace SuperMWeb.Application;

// Cabecera de venta para los reportes diario y por rango
// (ReporteDiarioForm / ReporteMensualForm).
public record ResumenVenta(int IdFactura, DateTime Fecha, decimal Total, string? Cajero);
