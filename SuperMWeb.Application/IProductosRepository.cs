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
}
