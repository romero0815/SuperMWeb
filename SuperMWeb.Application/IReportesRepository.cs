namespace SuperMWeb.Application;

// Consultas analiticas del modulo de reportes. Portadas de ReportesForm,
// ReporteDiario/MensualForm (ventas y compras), ReporteGananciasForm y
// DetalleFacturaForm del desktop.
//
// Los rangos de fecha son [desde, hasta) en el repositorio: el llamador
// pasa hasta = fechaFinElegida.AddDays(1) para que el ultimo dia quede
// incluido completo.
public interface IReportesRepository
{
    // --- Dashboard ---

    Task<ResumenInventario> ObtenerResumenInventarioAsync(CancellationToken ct = default);

    // TOP 10 por unidades vendidas en el mes calendario en curso.
    Task<IReadOnlyList<ProductoMasVendido>> ListarMasVendidosDelMesAsync(CancellationToken ct = default);

    // TOP 150 productos activos con Stock <= 5, ascendente.
    Task<IReadOnlyList<ProductoStockBajo>> ListarStockBajoAsync(CancellationToken ct = default);

    // TOP 150 productos activos con Stock <= 5 historicos sin importar su historial de ventas.
    Task<IReadOnlyList<ProductoStockBajo>> ListarStockBajoHistoricoAsync(CancellationToken ct = default);

    // --- Ventas ---

    Task<IReadOnlyList<ResumenVenta>> ListarVentasDelDiaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ResumenVenta>> ListarVentasEntreAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default);

    // --- Compras ---

    Task<IReadOnlyList<ResumenCompra>> ListarComprasDelDiaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ResumenCompra>> ListarComprasEntreAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default);

    // --- Ganancias ---

    Task<IReadOnlyList<FilaGanancia>> ListarGananciasEntreAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default);

    // --- Detalle de documento ---

    // esCompra = false lee Facturas/DetalleFacturas; true lee Compras/DetalleCompras.
    Task<IReadOnlyList<LineaDocumento>> ListarDetalleDocumentoAsync(
        int id, bool esCompra, CancellationToken ct = default);
}
