using System.Runtime.Versioning;
using SuperMWeb.Application;

namespace SuperMWeb.Infrastructure.Printers;

// Envia bytes RAW al spooler de Windows por medio de RawPrinterHelper.
// Busca la impresora por sustring en el nombre (default "POS", igual
// que hacia el desktop antiguamente).
[SupportedOSPlatform("windows")]
public sealed class LocalSpoolerPrinter : ITicketPrinter
{
    private readonly string _nombreParcial;

    public LocalSpoolerPrinter(string nombreParcial = "POS")
    {
        _nombreParcial = nombreParcial;
    }

    public Task<PrintResult> ImprimirAsync(byte[] bytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var impresora = RawPrinterHelper.BuscarImpresora(_nombreParcial);
        if (impresora == null)
        {
            return Task.FromResult(PrintResult.Falla(
                $"No se encontro ninguna impresora que contenga '{_nombreParcial}' en el nombre."));
        }

        var (exito, codigo) = RawPrinterHelper.SendBytesToPrinter(impresora, bytes);
        return Task.FromResult(exito
            ? PrintResult.Ok()
            : PrintResult.Falla($"Error al enviar bytes al spooler. Codigo Win32: {codigo}.", codigo));
    }
}
