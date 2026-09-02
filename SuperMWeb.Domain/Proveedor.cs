namespace SuperMWeb.Domain;

public class Proveedor
{
    public int IdProveedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? Correo { get; set; }
    public bool? Active { get; set; }
}
