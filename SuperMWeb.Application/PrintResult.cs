namespace SuperMWeb.Application;

public record PrintResult(bool Exitoso, string? MensajeError = null, int? CodigoError = null)
{
    public static PrintResult Ok() => new(true);
    public static PrintResult Falla(string mensaje, int? codigo = null) => new(false, mensaje, codigo);
}
