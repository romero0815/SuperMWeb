using Microsoft.Data.SqlClient;
using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Infrastructure.Data;

// Portado de ProveedoresForm y de FrmProductosForm.CargarProveedores.
public sealed class ProveedoresRepository : IProveedoresRepository
{
    private readonly SqlConnectionFactory _factory;

    public ProveedoresRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Proveedor>> ListarActivosAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT IdProveedor, Nombre, Telefono, Direccion, Correo, Active
            FROM Proveedores
            WHERE Active = 1
            ORDER BY Nombre";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var lista = new List<Proveedor>();
        while (await reader.ReadAsync(ct)) lista.Add(Leer(reader));
        return lista;
    }

    public async Task<Proveedor?> ObtenerPorIdAsync(int idProveedor, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT IdProveedor, Nombre, Telefono, Direccion, Correo, Active
            FROM Proveedores
            WHERE IdProveedor = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idProveedor));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Leer(reader) : null;
    }

    public async Task<int> CrearAsync(Proveedor proveedor, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO Proveedores (Nombre, Telefono, Direccion, Correo, Active)
            VALUES (@nombre, @telefono, @direccion, @correo, 1);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        AgregarCampos(cmd, proveedor);

        var id = await cmd.ExecuteScalarAsync(ct);
        return id is null || id == DBNull.Value ? 0 : Convert.ToInt32(id);
    }

    public async Task ActualizarAsync(Proveedor proveedor, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE Proveedores
            SET Nombre = @nombre, Telefono = @telefono, Direccion = @direccion, Correo = @correo
            WHERE IdProveedor = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        AgregarCampos(cmd, proveedor);
        cmd.Parameters.Add(new SqlParameter("@id", proveedor.IdProveedor));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DesactivarAsync(int idProveedor, CancellationToken ct = default)
    {
        const string sql = "UPDATE Proveedores SET Active = 0 WHERE IdProveedor = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idProveedor));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AgregarCampos(SqlCommand cmd, Proveedor p)
    {
        cmd.Parameters.Add(new SqlParameter("@nombre", p.Nombre));
        cmd.Parameters.Add(new SqlParameter("@telefono", (object?)p.Telefono ?? DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@direccion", (object?)p.Direccion ?? DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@correo", (object?)p.Correo ?? DBNull.Value));
    }

    private static Proveedor Leer(SqlDataReader r) => new()
    {
        IdProveedor = r.GetInt32(0),
        Nombre = r.GetString(1),
        Telefono = r.IsDBNull(2) ? null : r.GetString(2),
        Direccion = r.IsDBNull(3) ? null : r.GetString(3),
        Correo = r.IsDBNull(4) ? null : r.GetString(4),
        Active = r.IsDBNull(5) ? null : r.GetBoolean(5),
    };
}
