namespace SuperMWeb.Domain;

public class DetalleCompra
{
    public int IdDetalleCompra { get; set; }
    public int IdCompra { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    // En la BD es una columna computed PERSISTED (Cantidad * PrecioUnitario).
    public decimal Subtotal => Cantidad * PrecioUnitario;
}
