namespace SuperMWeb.Contracts;

// Respuesta de POST /api/ventas. Total calculado por el servidor.
// IdFactura solo si el SP lo devuelve; hoy no lo hace, queda 0 pero
// deja el hueco para el futuro (extender el SP para devolver SCOPE_IDENTITY).
public record RegistrarVentaResponse(
    int IdFactura,
    decimal Total,
    int CantidadItems);
