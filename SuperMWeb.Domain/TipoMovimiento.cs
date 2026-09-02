namespace SuperMWeb.Domain;

// La columna MovimientosInventario.TipoMovimiento tiene un CHECK que solo
// permite estos dos valores. Los "ajustes" reutilizan estos y se distinguen
// por Origen='Ajuste'.
public static class TipoMovimiento
{
    public const string Entrada = "Entrada";
    public const string Salida = "Salida";
}
