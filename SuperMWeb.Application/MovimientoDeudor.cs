using SuperMWeb.Domain;

namespace SuperMWeb.Application;

// Fila del estado de cuenta de un deudor (DeudorMovimientoForm).
public record MovimientoDeudor(
    int IdMovimiento,
    DateTime Fecha,
    string Tipo,
    decimal Valor,
    string? Descripcion,
    int? IdVenta,
    string? Usuario,
    bool Anulado)
{
    public bool EsCargo => Tipo == TipoMovimientoDeudor.Cargo;
    public string Etiqueta => EsCargo ? "Cargo" : "Abono";
}
