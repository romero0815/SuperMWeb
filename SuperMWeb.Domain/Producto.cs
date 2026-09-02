namespace SuperMWeb.Domain;

public class Producto
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Ultimo precio de compra pagado a proveedor. RegistrarCompra lo sobreescribe
    // con el PrecioUnitario de cada linea (ver docs/RegistrarCompra.sql).
    public decimal PrecioCompra { get; set; }

    public decimal PrecioVenta { get; set; }
    public int Stock { get; set; }
    public int? IdProveedor { get; set; }
    public bool? Active { get; set; }
    public string? CodigoBarras { get; set; }
}
