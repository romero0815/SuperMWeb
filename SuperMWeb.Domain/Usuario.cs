namespace SuperMWeb.Domain;

public class Usuario
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string UsuarioLogin { get; set; } = string.Empty;

    // Clave en texto plano en la BD (columna Usuarios.Clave). Ver R2 del inventario.
    public string Clave { get; set; } = string.Empty;

    public int IdRol { get; set; }
    public bool? Active { get; set; }
}
