namespace SuperMWeb.Contracts;

// Payload de POST /api/ventas. El servidor calcula el Total desde los
// items para evitar confiar en el cliente; se ignora cualquier total que
// pudiera venir en el body.
public record RegistrarVentaRequest(
    int IdUsuario,
    string? Cliente,
    IReadOnlyList<LineaVentaDto> Items);
