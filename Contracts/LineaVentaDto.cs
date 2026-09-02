namespace SuperMWeb.Contracts;

// Una linea del detalle enviada por el cliente al POST /api/ventas.
// Nombre viaja para que el ticket pueda imprimirse sin volver a la BD.
public record LineaVentaDto(
    int IdProducto,
    string Nombre,
    int Cantidad,
    decimal PrecioUnitario);
