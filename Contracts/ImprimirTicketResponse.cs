namespace SuperMWeb.Contracts;

public record ImprimirTicketResponse(
    bool Exitoso,
    string? MensajeError,
    int? CodigoError);
