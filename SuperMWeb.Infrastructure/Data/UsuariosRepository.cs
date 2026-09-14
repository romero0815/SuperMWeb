using Microsoft.Data.SqlClient;
using SuperMWeb.Application;
using SuperMWeb.Domain;

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

    // ---------- Gestion de usuarios ----------

    // El rol 3 (SuperAdmin) queda fuera: el desktop no lo lista ni lo ofrece.
    private const int RolSuperAdmin = 3;

    public async Task<IReadOnlyList<UsuarioListado>> ListarAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT u.IdUsuario, u.Nombre, u.Usuario, r.NombreRol
            FROM Usuarios u
            INNER JOIN Roles r ON u.IdRol = r.IdRol
            WHERE u.Active = 1 AND r.IdRol <> @rolExcluido
            ORDER BY u.Nombre";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@rolExcluido", RolSuperAdmin));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<UsuarioListado>();
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new UsuarioListado(
                IdUsuario: reader.GetInt32(0),
                Nombre: reader.GetString(1),
                Usuario: reader.GetString(2),
                NombreRol: reader.GetString(3)));
        }
        return lista;
    }

    public async Task<IReadOnlyList<Rol>> ListarRolesAsignablesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT IdRol, NombreRol FROM Roles WHERE IdRol <> @rolExcluido ORDER BY NombreRol";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@rolExcluido", RolSuperAdmin));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<Rol>();
        while (await reader.ReadAsync(ct))
            lista.Add(new Rol { IdRol = reader.GetInt32(0), NombreRol = reader.GetString(1) });
        return lista;
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int idUsuario, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT u.IdUsuario, u.Nombre, u.Usuario, u.Clave, u.IdRol, u.Active
            FROM Usuarios u
            WHERE u.IdUsuario = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idUsuario));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new Usuario
        {
            IdUsuario = reader.GetInt32(0),
            Nombre = reader.GetString(1),
            UsuarioLogin = reader.GetString(2),
            Clave = reader.GetString(3),
            IdRol = reader.GetInt32(4),
            Active = reader.IsDBNull(5) ? null : reader.GetBoolean(5)
        };
    }

    public async Task<int> CrearAsync(Usuario usuario, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO Usuarios (Usuario, Clave, IdRol, Nombre, Active)
            VALUES (@usuario, @clave, @idRol, @nombre, 1);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        AgregarCampos(cmd, usuario);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id is null || id == DBNull.Value ? 0 : Convert.ToInt32(id);
    }

    public async Task ActualizarAsync(Usuario usuario, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE Usuarios
            SET Usuario = @usuario, Clave = @clave, IdRol = @idRol, Nombre = @nombre
            WHERE IdUsuario = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        AgregarCampos(cmd, usuario);
        cmd.Parameters.Add(new SqlParameter("@id", usuario.IdUsuario));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DesactivarAsync(int idUsuario, CancellationToken ct = default)
    {
        const string sql = "UPDATE Usuarios SET Active = 0 WHERE IdUsuario = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idUsuario));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> ExisteUsuarioAsync(
        string usuario, int? idExcluir = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(usuario)) return false;

        var sql = "SELECT COUNT(*) FROM Usuarios WHERE Usuario = @usuario AND Active = 1";
        if (idExcluir is not null) sql += " AND IdUsuario <> @excluir";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@usuario", usuario.Trim()));
        if (idExcluir is not null) cmd.Parameters.Add(new SqlParameter("@excluir", idExcluir.Value));

        var res = await cmd.ExecuteScalarAsync(ct);
        return res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0;
    }

    private static void AgregarCampos(SqlCommand cmd, Usuario u)
    {
        cmd.Parameters.Add(new SqlParameter("@usuario", u.UsuarioLogin));
        cmd.Parameters.Add(new SqlParameter("@clave", u.Clave));
        cmd.Parameters.Add(new SqlParameter("@idRol", u.IdRol));
        cmd.Parameters.Add(new SqlParameter("@nombre", u.Nombre));
    }
}
