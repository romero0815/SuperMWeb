using Microsoft.Data.SqlClient;
using SuperMWeb.Application;

namespace SuperMWeb.Infrastructure.Data;

public sealed class UsuariosRepository : IUsuariosRepository
{
    private readonly SqlConnectionFactory _factory;

    public UsuariosRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<UsuarioAutenticado?> AutenticarAsync(string usuario, string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(clave))
            return null;

        // Query identica a la de SuperM.LoginForm.cs.
        const string sql = @"
            SELECT u.IdUsuario, u.Usuario, r.NombreRol, u.Nombre
            FROM Usuarios u
            INNER JOIN Roles r ON u.IdRol = r.IdRol
            WHERE u.Usuario = @usuario AND u.Clave = @clave AND u.Active = 1";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@usuario", usuario));
        cmd.Parameters.Add(new SqlParameter("@clave", clave));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new UsuarioAutenticado(
            IdUsuario: reader.GetInt32(0),
            Usuario: reader.GetString(1),
            Rol: reader.GetString(2),
            Nombre: reader.GetString(3));
    }
}
