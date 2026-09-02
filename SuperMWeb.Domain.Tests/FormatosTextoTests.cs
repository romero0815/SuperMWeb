using System.Globalization;
using SuperMWeb.Application;

namespace SuperMWeb.Domain.Tests;

// Caracteriza los helpers de formato/parseo bajo cultura es-CO (caja real).
public class FormatosTextoTests : IDisposable
{
    private readonly CultureInfo _culturaOriginal;

    public FormatosTextoTests()
    {
        _culturaOriginal = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("es-CO");
    }

    public void Dispose()
    {
        Thread.CurrentThread.CurrentCulture = _culturaOriginal;
    }

    // ---------- FormatoMoneda(decimal) ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1500)]
    [InlineData(12345678)]
    public void FormatoMoneda_incluye_simbolo_peso(decimal valor)
    {
        // No pinamos el formato exacto (varia entre versiones de .NET/ICU),
        // pero el simbolo de peso debe estar. Es lo unico que la app usa despues
        // para el parseo inverso (ver ReplaceCaracteres del desktop).
        var s = FormatosTexto.FormatoMoneda(valor);
        Assert.Contains("$", s);
    }

    // ---------- FormatoMoneda(string) ----------

    [Fact]
    public void FormatoMoneda_string_valido_formatea()
    {
        Assert.Contains("$", FormatosTexto.FormatoMoneda("1500"));
    }

    [Fact]
    public void FormatoMoneda_string_invalido_devuelve_vacio()
    {
        Assert.Equal(string.Empty, FormatosTexto.FormatoMoneda("no-numero"));
    }

    // ---------- ParseDecimal ----------

    [Theory]
    [InlineData("1500", 1500)]
    [InlineData("0", 0)]
    [InlineData("1234,56", 1234.56)]  // es-CO usa coma decimal
    public void ParseDecimal_valores_validos(string entrada, decimal esperado)
    {
        Assert.Equal(esperado, FormatosTexto.ParseDecimal(entrada));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData(null)]
    public void ParseDecimal_valor_invalido_devuelve_cero(string? entrada)
    {
        Assert.Equal(0m, FormatosTexto.ParseDecimal(entrada!));
    }

    // ---------- ValidEmail ----------

    [Theory]
    [InlineData("a@b.co")]
    [InlineData("nombre.apellido@dominio.com")]
    [InlineData("user+tag@sub.dominio.co")]
    public void ValidEmail_correos_validos(string email)
    {
        Assert.True(FormatosTexto.ValidEmail(email));
    }

    [Theory]
    [InlineData("no-arroba")]
    [InlineData("a@")]
    [InlineData("@b.co")]
    [InlineData("a@b")]           // sin TLD
    [InlineData("a@b.c")]         // TLD de 1 letra
    public void ValidEmail_correos_invalidos(string email)
    {
        Assert.False(FormatosTexto.ValidEmail(email));
    }

    // ---------- IsNumber ----------

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("1234567890")]
    public void IsNumber_enteros_validos(string s)
    {
        Assert.True(FormatosTexto.IsNumber(s));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("abc")]
    [InlineData("1 2")]
    public void IsNumber_no_enteros_invalidos(string s)
    {
        Assert.False(FormatosTexto.IsNumber(s));
    }

    // ---------- IsNumberDecimal ----------
    // El desktop acepta el patron ^\d+(,\d+)?$ que es "entero opcional , decimal".
    // NO acepta punto decimal ni negativos.

    [Theory]
    [InlineData("0")]
    [InlineData("1500")]
    [InlineData("1234,56")]
    [InlineData("0,99")]
    public void IsNumberDecimal_validos(string s)
    {
        Assert.True(FormatosTexto.IsNumberDecimal(s));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1.5")]     // punto como decimal NO se acepta
    [InlineData("1,")]      // coma sin decimales
    [InlineData(",5")]      // sin parte entera
    [InlineData("abc")]
    public void IsNumberDecimal_invalidos(string s)
    {
        Assert.False(FormatosTexto.IsNumberDecimal(s));
    }
}
