namespace SuperMWeb.Domain;

public class Compra
{
    public int IdCompra { get; set; }
    public DateTime Fecha { get; set; }
    public int IdProveedor { get; set; }
    public int IdUsuario { get; set; }
    public decimal Total { get; set; }
}
