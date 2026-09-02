namespace SuperMWeb.Domain;

public class DetalleFactura
{
    public int IdDetalleFactura { get; set; }
    public int IdFactura { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    // En la BD es una columna computed PERSISTED (Cantidad * PrecioUnitario).
    // Aqui es calculada para reflejar el mismo contrato desde el dominio.
    public decimal Subtotal => Cantidad * PrecioUnitario;
}
