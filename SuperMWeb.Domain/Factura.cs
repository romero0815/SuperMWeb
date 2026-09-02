namespace SuperMWeb.Domain;

public class Factura
{
    public int IdFactura { get; set; }
    public DateTime Fecha { get; set; }
    public int IdUsuario { get; set; }
    public string? Cliente { get; set; }
    public decimal Total { get; set; }
}
