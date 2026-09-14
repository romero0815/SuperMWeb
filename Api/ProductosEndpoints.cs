using SuperMWeb.Application;

namespace SuperMWeb.Api;

// Descargas del modulo de productos. Van como endpoints y no por DI en el
// componente porque el navegador necesita una URL real para guardar el archivo:
// es el equivalente del SaveFileDialog que usaba el desktop.
public static class ProductosEndpoints
{
    public static IEndpointRouteBuilder MapProductosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/productos")
                       .WithTags("Productos")
                       .RequireAuthorization();

        grupo.MapGet("/plantilla", DescargarPlantilla)
             .WithName("DescargarPlantillaProductos")
             .WithSummary("Plantilla .xlsx vacia para la carga masiva de productos.");

        grupo.MapGet("/exportar", ExportarProductos)
             .WithName("ExportarProductos")
             .WithSummary("Exporta los productos activos a .xlsx.");

        return app;
    }

    private static IResult DescargarPlantilla(IProductosExcel excel) =>
        Results.File(
            excel.GenerarPlantilla(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "CargaProductos.xlsx");

    private static async Task<IResult> ExportarProductos(
        IProductosRepository repo,
        IProductosExcel excel,
        CancellationToken ct)
    {
        // limite null: el desktop exportaba todos los activos, sin el TOP 250
        // que si aplicaba a la grilla.
        var productos = await repo.ListarAsync(limite: null, ct: ct);
        var bytes = excel.Exportar(productos);

        return Results.File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ProductosActivos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }
}
