using SuperMWeb.Application;

namespace SuperMWeb.Infrastructure.Printers;

// Impresora de mentiras para desarrollo/tests sin hardware.
// Descarta los bytes; siempre devuelve exito. Opcionalmente los expone
// via la propiedad UltimoTicket para inspeccion en tests.
public sealed class NullTicketPrinter : ITicketPrinter
{
    public byte[]? UltimoTicket { get; private set; }
    public int TotalImpresiones { get; private set; }

    public Task<PrintResult> ImprimirAsync(byte[] bytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        UltimoTicket = bytes;
        TotalImpresiones++;
        return Task.FromResult(PrintResult.Ok());
    }
}
