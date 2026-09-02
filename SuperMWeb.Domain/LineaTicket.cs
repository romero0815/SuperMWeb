namespace SuperMWeb.Domain;

// Vista simplificada de una linea del detalle para impresion del ticket
// y para construir el TVP que se envia a los stored procedures
// RegistrarVenta y RegistrarCompra. Equivale al antiguo
// SuperM.Clases.Modelos.DetalleFacturas del desktop.
public class LineaTicket
{
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}
