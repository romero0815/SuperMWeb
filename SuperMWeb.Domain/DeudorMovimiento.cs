namespace SuperMWeb.Domain;

// Movimiento de la cuenta de un deudor: un cargo ('C') o un abono ('A').
// El saldo nunca se almacena: se calcula sumando cargos y restando abonos
// no anulados.
public class DeudorMovimiento
{
    public int IdMovimiento { get; set; }
    public int IdDeudor { get; set; }

    // Ver TipoMovimientoDeudor. CHECK en BD: solo 'C' o 'A'.
    public string Tipo { get; set; } = TipoMovimientoDeudor.Cargo;

    // CHECK en BD: siempre mayor que cero.
    public decimal Valor { get; set; }

    // La columna existe pero el desktop nunca la llena, ni siquiera al fiar
    // una venta. Se mantiene por compatibilidad.
    public int? IdVenta { get; set; }

    public string? Descripcion { get; set; }
    public DateTime Fecha { get; set; }

    // Nombre de usuario del cajero, como texto libre. No hay FK a Usuarios.
    public string? Usuario { get; set; }

    // Anular no borra: marca el movimiento y lo saca del calculo del saldo.
    public bool Anulado { get; set; }
}
