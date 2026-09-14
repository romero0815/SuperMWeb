namespace SuperMWeb.Application;

// Resultado de una carga masiva desde Excel. A diferencia del desktop, que
// abortaba en el primer error dejando insertadas las filas anteriores, aqui
// se valida el archivo completo antes de insertar nada: si Errores tiene
// elementos, Insertados es 0 y la base no se toco.
public record ResultadoImportacion(int Insertados, IReadOnlyList<string> Errores)
{
    public bool Exitoso => Errores.Count == 0;
}
