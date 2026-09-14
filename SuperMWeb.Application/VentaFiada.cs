namespace SuperMWeb.Application;

// Datos que la pantalla de fiado entrega para registrar la venta y la deuda
// en una sola transaccion (equivale a lo que FrmFiarVenta devolvia a
// VentasForm en el desktop).
//
// IdDeudor > 0 usa un cliente existente. Con IdDeudor = 0 se crea el deudor
// con los datos de NombreCliente/Identificacion/Telefono/CupoMaximo.
public record VentaFiada(
    int IdDeudor,
    string NombreCliente,
    string? Identificacion,
    string? Telefono,
    decimal? CupoMaximo,
    decimal Valor,
    string? Descripcion)
{
    public bool EsClienteNuevo => IdDeudor <= 0;
}
