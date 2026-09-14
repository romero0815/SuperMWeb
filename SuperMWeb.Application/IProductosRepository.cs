using SuperMWeb.Domain;

namespace SuperMWeb.Application;

public interface IProductosRepository
{
    // Busca un producto activo por su codigo de barras exacto. Null si no existe.
    Task<Producto?> BuscarPorCodigoBarrasAsync(string codigo, CancellationToken ct = default);

    // Devuelve TOP 250 productos activos con stock > 0 (mismo criterio que
    // VentasForm.CargarProductos: los que efectivamente pueden venderse).
    Task<IReadOnlyList<Producto>> ListarDisponiblesParaVentaAsync(CancellationToken ct = default);

    // Devuelve el Stock actual de un producto (para validar antes de agregar
    // al detalle de venta). Devuelve 0 si el producto no existe.
    Task<int> ObtenerStockAsync(int idProducto, CancellationToken ct = default);

    // --- Gestion de productos (ProductosForm / FrmProductosForm) ---

    // Listado de productos activos con el proveedor resuelto.
    // Sin filtro devuelve TOP 250; con filtroNombre busca por LIKE sobre el
    // nombre; con codigoBarras busca la coincidencia exacta. Los dos filtros
    // son excluyentes, igual que en el desktop.
    // limite = null trae todas las filas: lo usa la exportacion a Excel, que
    // en el desktop tampoco aplicaba el TOP de la grilla.
    Task<IReadOnlyList<ProductoListado>> ListarAsync(
        string? filtroNombre = null,
        string? codigoBarras = null,
        int? limite = 250,
        CancellationToken ct = default);

    Task<Producto?> ObtenerPorIdAsync(int idProducto, CancellationToken ct = default);

    // Inserta con Active = 1 y devuelve el IdProducto generado.
    Task<int> CrearAsync(Producto producto, CancellationToken ct = default);

    Task ActualizarAsync(Producto producto, CancellationToken ct = default);

    // Soft-delete: Active = 0 (el desktop nunca borra productos fisicamente).
    Task DesactivarAsync(int idProducto, CancellationToken ct = default);

    // True si ya existe un producto activo con ese codigo de barras.
    // idExcluir permite ignorar el propio producto al editar.
    Task<bool> ExisteCodigoBarrasAsync(
        string codigoBarras,
        int? idExcluir = null,
        CancellationToken ct = default);
}
