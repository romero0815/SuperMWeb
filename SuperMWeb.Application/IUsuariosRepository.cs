namespace SuperMWeb.Application;

public interface IUsuariosRepository
{
    // Devuelve el usuario si las credenciales son validas y el usuario esta
    // activo. Null en cualquier otro caso. Portado tal cual de LoginForm.cs
    // del desktop (clave en texto plano; ver R2 del inventario).
    Task<UsuarioAutenticado?> AutenticarAsync(string usuario, string clave, CancellationToken ct = default);
}
