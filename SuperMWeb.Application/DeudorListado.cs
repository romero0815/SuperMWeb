namespace SuperMWeb.Application;

// Fila de la grilla de deudores, con el saldo ya calculado por la consulta.
public record DeudorListado(
    int IdDeudor,
    string NombreCliente,
    string? Identificacion,
    string? Telefono,
    string? Direccion,
    decimal? CupoMaximo,
    decimal Saldo,
    string? Observaciones,
    DateTime FechaCreacion,
    DateTime? FechaModificacion)
{
    // El desktop pinta el saldo en rojo cuando supera el cupo asignado.
    // Un cupo nulo o en cero significa "sin limite".
    public bool SuperaCupo => CupoMaximo is > 0 && Saldo > CupoMaximo.Value;
}
