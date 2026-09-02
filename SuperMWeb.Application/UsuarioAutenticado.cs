namespace SuperMWeb.Application;

// Vista del usuario cuando ya paso el login. Incluye el nombre del rol
// resuelto por JOIN a Roles, que es lo que la UI necesita para aplicar
// permisos textuales (Administrador/Cajero/SuperAdmin) igual que el desktop.
public record UsuarioAutenticado(
    int IdUsuario,
    string Usuario,
    string Nombre,
    string Rol);
