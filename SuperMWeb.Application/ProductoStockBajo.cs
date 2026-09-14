namespace SuperMWeb.Application;

// Fila de la tabla de stock minimo del dashboard: productos activos
// con Stock <= 5 (ReportesForm.CargarProductosConStockBajo).
public record ProductoStockBajo(string Nombre, int Stock);
