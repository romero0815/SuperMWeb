using System.Text.RegularExpressions;

namespace SuperMWeb.Application;

// Helpers de formato y parseo extraidos de SuperM.Clases.Helpers.Utils
// sin cambio de comportamiento. La cultura de format ("{0:C}") sigue
// dependiendo de la cultura actual del hilo; en produccion se fija en
// es-CO. Las regex y su comportamiento son identicos al desktop.
public static class FormatosTexto
{
    public static string FormatoMoneda(decimal valor)
    {
        return string.Format("{0:C}", valor);
    }

    public static string FormatoMoneda(string valor)
    {
        if (decimal.TryParse(valor, out decimal resultado))
        {
            return FormatoMoneda(resultado);
        }
        return string.Empty;
    }

    public static decimal ParseDecimal(string valor)
    {
        if (decimal.TryParse(valor, out decimal resultado))
        {
            return resultado;
        }
        return 0;
    }

    public static bool ValidEmail(string valor)
    {
        try
        {
            string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(valor, pattern, RegexOptions.IgnoreCase);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public static bool IsNumber(string valor)
    {
        try
        {
            string pattern = @"^\d+$";
            return Regex.IsMatch(valor, pattern, RegexOptions.IgnoreCase);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public static bool IsNumberDecimal(string valor)
    {
        try
        {
            string pattern = @"^\d+(,\d+)?$";
            return Regex.IsMatch(valor, pattern, RegexOptions.IgnoreCase);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
