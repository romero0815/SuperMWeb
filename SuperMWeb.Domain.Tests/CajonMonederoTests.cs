using SuperMWeb.Application;

namespace SuperMWeb.Domain.Tests;

// Apertura del cajon monedero via pulso ESC/POS por la impresora.
public class CajonMonederoTests
{
    private sealed class ImpresoraFalsa : ITicketPrinter
    {
        public List<byte[]> Enviados { get; } = new();

        public Task<PrintResult> ImprimirAsync(byte[] bytes, CancellationToken ct = default)
        {
            Enviados.Add(bytes);
            return Task.FromResult(PrintResult.Ok());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ConstruirAperturaCajon_genera_ESC_p_con_el_pin(byte pin)
    {
        Assert.Equal(new byte[] { 0x1B, 0x70, pin, 0x19, 0xFA }, TicketBuilder.ConstruirAperturaCajon(pin));
    }

    [Fact]
    public void ConstruirAperturaCajon_rechaza_pin_invalido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TicketBuilder.ConstruirAperturaCajon(2));
    }

    [Fact]
    public async Task AbrirAsync_habilitado_envia_el_pulso_a_la_impresora()
    {
        var impresora = new ImpresoraFalsa();
        var cajon = new CajonMonedero(impresora, habilitado: true, pin: 0);

        var res = await cajon.AbrirAsync();

        Assert.True(res.Exitoso);
        Assert.Single(impresora.Enviados);
        Assert.Equal(TicketBuilder.ConstruirAperturaCajon(0), impresora.Enviados[0]);
    }

    [Fact]
    public async Task AbrirAsync_deshabilitado_no_envia_nada()
    {
        var impresora = new ImpresoraFalsa();
        var cajon = new CajonMonedero(impresora, habilitado: false);

        var res = await cajon.AbrirAsync();

        Assert.True(res.Exitoso);
        Assert.Empty(impresora.Enviados);
    }
}
