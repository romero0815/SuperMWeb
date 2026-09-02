namespace SuperMWeb.Application;

// Envio de bytes ESC/POS a la impresora fisica. Implementaciones:
// - LocalSpoolerPrinter (Infrastructure): P/Invoke a winspool.drv.
// - NullTicketPrinter (Infrastructure): log, para desarrollo sin hardware.
public interface ITicketPrinter
{
    Task<PrintResult> ImprimirAsync(byte[] bytes, CancellationToken ct = default);
}
