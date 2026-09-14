namespace SuperMWeb.Application;

// Carga y descarga masiva de productos en Excel. Equivale a
// DataUtil.ImportarExcelAGeneral / ExportarTablaAExcel del desktop,
// mas la plantilla que el desktop distribuia como archivo suelto
// en Plantillas/CargaProductos.xlsx.
public interface IProductosExcel
{
    // Plantilla vacia con las cabeceras que espera la importacion.
    byte[] GenerarPlantilla();

    byte[] Exportar(IReadOnlyList<ProductoListado> productos);

    // Lee el .xlsx, valida todas las filas y solo entonces inserta.
    Task<ResultadoImportacion> ImportarAsync(Stream archivo, CancellationToken ct = default);
}
