using Microsoft.AspNetCore.Mvc;
using SuperMWeb.Application;
using SuperMWeb.Contracts;
using SuperMWeb.Domain;

namespace SuperMWeb.Api;

// Endpoints Minimal API para el flujo de venta.
// La UI Blazor NO los consume: inyecta los servicios directamente por DI.
// Estos endpoints son para consumo externo: segunda caja futura, scripts,
// coleccion .http de docs, tests HTTP.
public static class VentaEndpoints
{
    public static IEndpointRouteBuilder MapVentaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api").WithTags("Venta");

        grupo.MapGet("/productos/por-codigo/{barras}", BuscarPorCodigo)
             .WithName("BuscarProductoPorCodigo")
             .WithSummary("Busca un producto activo por su codigo de barras exacto.");

        grupo.MapGet("/productos/disponibles", ListarDisponibles)
             .WithName("ListarProductosDisponibles")
             .WithSummary("Devuelve TOP 250 productos activos con stock > 0.");

        grupo.MapPost("/ventas", RegistrarVenta)
             .WithName("RegistrarVenta")
             .WithSummary("Registra una venta y descuenta stock via trigger.");

        grupo.MapPost("/ventas/imprimir", ImprimirTicket)
             .WithName("ImprimirTicketVenta")
             .WithSummary("Genera y envia el ticket ESC/POS a la impresora configurada.");

        return app;
    }

    private static async Task<IResult> BuscarPorCodigo(
        string barras,
        IProductosRepository repo,
        CancellationToken ct)
    {
        var p = await repo.BuscarPorCodigoBarrasAsync(barras, ct);
        if (p is null) return Results.NotFound();
        return Results.Ok(ToDto(p));
    }

    private static async Task<IResult> ListarDisponibles(
        IProductosRepository repo,
        CancellationToken ct)
    {
        var lista = await repo.ListarDisponiblesParaVentaAsync(ct);
        return Results.Ok(lista.Select(ToDto));
    }

    private static async Task<IResult> RegistrarVenta(
        [FromBody] RegistrarVentaRequest req,
        IVentasRepository ventas,
        CancellationToken ct)
    {
        if (req is null) return Results.BadRequest(new { error = "Body vacio." });
        if (req.Items is null || req.Items.Count == 0)
            return Results.BadRequest(new { error = "La venta debe tener al menos una linea." });

        var items = req.Items
            .Select(l => new LineaTicket
            {
                IdProducto = l.IdProducto,
                Nombre = l.Nombre,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario
            });

        var agrupado = VentaCalculadora.AgruparDetalle(items);
        var total = VentaCalculadora.CalcularTotal(agrupado);

        await ventas.RegistrarVentaAsync(req.IdUsuario, req.Cliente ?? string.Empty, total, agrupado, ct);

        // El SP RegistrarVenta actualmente no devuelve el IdFactura. Se retorna 0
        // hasta que el SP se extienda para devolver SCOPE_IDENTITY(); el cliente
        // aun puede reimprimir enviando los items via /api/ventas/imprimir.
        return Results.Ok(new RegistrarVentaResponse(0, total, agrupado.Count));
    }

    private static async Task<IResult> ImprimirTicket(
        [FromBody] ImprimirTicketRequest req,
        ITicketPrinter printer,
        IConfiguration cfg,
        CancellationToken ct)
    {
        if (req is null || req.Items is null || req.Items.Count == 0)
            return Results.BadRequest(new { error = "El ticket debe tener al menos una linea." });

        var items = req.Items.Select(l => new LineaTicket
        {
            IdProducto = l.IdProducto,
            Nombre = l.Nombre,
            Cantidad = l.Cantidad,
            PrecioUnitario = l.PrecioUnitario
        }).ToList();

        var nombreNegocio = cfg["Negocio:Nombre"] ?? string.Empty;
        var bytes = TicketBuilder.ConstruirFactura(items, req.Total, nombreNegocio);

        var res = await printer.ImprimirAsync(bytes, ct);
        return Results.Ok(new ImprimirTicketResponse(res.Exitoso, res.MensajeError, res.CodigoError));
    }

    private static ProductoDto ToDto(Producto p) => new(
        p.IdProducto, p.Nombre, p.Descripcion, p.PrecioVenta, p.PrecioCompra, p.Stock, p.CodigoBarras);
}
