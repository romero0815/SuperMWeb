using SuperMWeb.Domain;

namespace SuperMWeb.Application;

public interface IProveedoresRepository
{
    // Proveedores con Active = 1, ordenados por nombre. Incluye el registro
    // centinela "Ninguno" que el desktop usa como valor por defecto.
    Task<IReadOnlyList<Proveedor>> ListarActivosAsync(CancellationToken ct = default);

    Task<Proveedor?> ObtenerPorIdAsync(int idProveedor, CancellationToken ct = default);

    Task<int> CrearAsync(Proveedor proveedor, CancellationToken ct = default);

    Task ActualizarAsync(Proveedor proveedor, CancellationToken ct = default);

    // Soft-delete: Active = 0. El desktop nunca borra proveedores fisicamente.
    Task DesactivarAsync(int idProveedor, CancellationToken ct = default);
}
