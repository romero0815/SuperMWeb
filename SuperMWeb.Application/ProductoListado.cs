namespace SuperMWeb.Application;

// Fila de la grilla de productos (ProductosForm del desktop): el producto
// con el nombre del proveedor resuelto por LEFT JOIN.
public record ProductoListado(
    int IdProducto,
    string Nombre,
    string? Descripcion,
    decimal PrecioCompra,
    decimal PrecioVenta,
    int Stock,
    string? Proveedor);
