namespace SuperMWeb.Domain;

// Cliente al que se le fia. Tabla Deudor, agregada al desktop despues de que
// se escribio el inventario de la Fase 0.
public class Deudor
{
    public int IdDeudor { get; set; }
    public string NombreCliente { get; set; } = string.Empty;

    // Indice unico filtrado UX_Deudor_Identificacion: unico cuando no es NULL.
    public string? Identificacion { get; set; }

    public string? Telefono { get; set; }
    public string? Direccion { get; set; }

    // NULL = sin limite de credito. Solo se valida cuando es mayor que cero.
    public decimal? CupoMaximo { get; set; }

    public string? Observaciones { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}
