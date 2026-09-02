namespace SuperMWeb.Domain;

public class MovimientoInventario
{
    public int IdMovimiento { get; set; }
    public int IdProducto { get; set; }
    public DateTime Fecha { get; set; }

    // "Entrada" o "Salida" segun el CHECK constraint. Ver TipoMovimiento.
    public string TipoMovimiento { get; set; } = Domain.TipoMovimiento.Entrada;

    public int Cantidad { get; set; }
    public string? Origen { get; set; }
    public int? IdReferencia { get; set; }
    public string? Observaciones { get; set; }
}
