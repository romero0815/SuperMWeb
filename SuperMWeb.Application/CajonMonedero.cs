namespace SuperMWeb.Application;

// Cajon monedero conectado por RJ11 al puerto DK de la impresora termica
// (DigitalPOS DIG-KR410 ECO). No tiene driver propio: se abre enviando el
// pulso ESC/POS por la misma impresora, asi que reutiliza ITicketPrinter.
// Con Habilitado=false no envia nada y responde exito, para cajas sin cajon.
public sealed class CajonMonedero
{
    private readonly ITicketPrinter _printer;
    private readonly bool _habilitado;
    private readonly byte _pin;

    public CajonMonedero(ITicketPrinter printer, bool habilitado, byte pin = 0)
    {
        ArgumentNullException.ThrowIfNull(printer);
        _printer = printer;
        _habilitado = habilitado;
        _pin = pin;
    }

    public Task<PrintResult> AbrirAsync(CancellationToken ct = default)
    {
        if (!_habilitado)
            return Task.FromResult(PrintResult.Ok());

        return _printer.ImprimirAsync(TicketBuilder.ConstruirAperturaCajon(_pin), ct);
    }
}
