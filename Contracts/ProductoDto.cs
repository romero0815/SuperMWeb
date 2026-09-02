namespace SuperMWeb.Contracts;

// Vista publica de Producto para el API. Omite Active (siempre true en
// resultados) y IdProveedor (no relevante para el flujo de venta).
public record ProductoDto(
    int IdProducto,
    string Nombre,
    string? Descripcion,
    decimal PrecioVenta,
    decimal PrecioCompra,
    int Stock,
    string? CodigoBarras);
