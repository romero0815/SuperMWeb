using SuperMWeb.Domain;

namespace SuperMWeb.Application;

public interface IUsuariosRepository
{
    // Devuelve el usuario si las credenciales son validas y el usuario esta
    // activo. Null en cualquier otro caso. Portado tal cual de LoginForm.cs
    // del desktop (clave en texto plano; ver R2 del inventario).
    Task<UsuarioAutenticado?> AutenticarAsync(string usuario, string clave, CancellationToken ct = default);

    // --- Gestion de usuarios (UsuariosForm) ---

    // Usuarios activos, excluyendo el rol 3 (SuperAdmin), igual que el
    // desktop: la cuenta de superadministrador no se administra desde la UI.
    Task<IReadOnlyList<UsuarioListado>> ListarAsync(CancellationToken ct = default);

    // Roles asignables, tambien sin el rol 3.
    Task<IReadOnlyList<Rol>> ListarRolesAsignablesAsync(CancellationToken ct = default);

    Task<Usuario?> ObtenerPorIdAsync(int idUsuario, CancellationToken ct = default);
    Task<Usuario?> ObtenerPorUsuarioAsync(string usuario, CancellationToken ct = default);

    // La clave se guarda en texto plano, igual que el desktop (R2 del
    // inventario). Es deuda tecnica conocida, no un descuido de la migracion.
    Task<int> CrearAsync(Usuario usuario, CancellationToken ct = default);

    Task ActualizarAsync(Usuario usuario, CancellationToken ct = default);
    Task ActualizarContraseñaAsync(int idUsuario, string nuevaClave, CancellationToken ct = default);

    // Soft-delete: Active = 0.
    Task DesactivarAsync(int idUsuario, CancellationToken ct = default);

    // True si ya existe un usuario activo con ese nombre de usuario.
    Task<bool> ExisteUsuarioAsync(string usuario, int? idExcluir = null, CancellationToken ct = default);
}
