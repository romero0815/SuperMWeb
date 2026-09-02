namespace SuperMWeb.Contracts;

// Payload de POST /api/ventas/imprimir (o /reimprimir). El servidor
// reconstruye el ESC/POS con TicketBuilder + envia via ITicketPrinter.
// El front NUNCA genera ESC/POS (regla del prompt).
public record ImprimirTicketRequest(
    IReadOnlyList<LineaVentaDto> Items,
    decimal Total);
