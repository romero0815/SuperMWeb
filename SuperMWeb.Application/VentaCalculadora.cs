using SuperMWeb.Domain;

namespace SuperMWeb.Application;

// Calculos de venta extraidos de SuperM.VentasForm sin cambiar comportamiento.
// Regla 1 del prompt: mover, no mejorar.
public static class VentaCalculadora
{
    // Suma de Subtotal de todos los items. Equivale a lo que hace el desktop
    // acumulando en un Label; aqui se calcula de una vez desde la lista.
    public static decimal CalcularTotal(IEnumerable<LineaTicket> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        decimal total = 0m;
        foreach (var i in items) total += i.Subtotal;
        return total;
    }

    // Cambio = recibido - total, con clamp a 0 si el recibido no alcanza.
    // Reproduce exactamente VentasForm.cs:391-395 y ComprasForm.cs:411-415.
    public static decimal CalcularCambio(decimal recibido, decimal total)
    {
        if (recibido < total) return 0m;
        return recibido - total;
    }

    // Agrupa el detalle por IdProducto/Nombre/PrecioUnitario sumando cantidades.
    // Reproduce exactamente VentasForm.cs:312-321 (GroupBy anonimo + Select).
    // Se usa antes de enviar el TVP al SP RegistrarVenta.
    public static IReadOnlyList<LineaTicket> AgruparDetalle(IEnumerable<LineaTicket> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .GroupBy(d => new { d.IdProducto, d.Nombre, d.PrecioUnitario })
            .Select(g => new LineaTicket
            {
                IdProducto = g.Key.IdProducto,
                Nombre = g.Key.Nombre,
                PrecioUnitario = g.Key.PrecioUnitario,
                Cantidad = g.Sum(x => x.Cantidad),
            })
            .ToList();
    }
}
