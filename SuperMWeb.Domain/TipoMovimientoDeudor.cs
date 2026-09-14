namespace SuperMWeb.Domain;

// La columna DeudorMovimiento.Tipo tiene un CHECK que solo acepta estos dos
// valores. Cargo suma al saldo, Abono lo resta.
public static class TipoMovimientoDeudor
{
    public const string Cargo = "C";
    public const string Abono = "A";
}
