using Microsoft.Data.SqlClient;
using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Infrastructure.Data;

// Modulo de fiado. Portado de DeudoresForm, DeudorMovimientoForm,
// FrmAgregarDeuda y FrmAgregarAbono del desktop.
public sealed class DeudoresRepository : IDeudoresRepository
{
    private readonly SqlConnectionFactory _factory;

    public DeudoresRepository(SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    // El saldo se calcula sumando cargos ('C') y restando abonos ('A')
    // no anulados. Nunca se persiste.
    private const string SqlListadoBase = @"
        SELECT d.IdDeudor, d.NombreCliente, d.Identificacion, d.Telefono, d.Direccion,
               d.CupoMaximo, ISNULL(m.Saldo, 0) AS Saldo, d.Observaciones,
               d.FechaCreacion, d.FechaModificacion
        FROM Deudor d
        OUTER APPLY (SELECT SUM(CASE WHEN mo.Tipo = 'C' THEN mo.Valor ELSE -mo.Valor END) AS Saldo
                     FROM DeudorMovimiento mo
                     WHERE mo.IdDeudor = d.IdDeudor AND mo.Anulado = 0) m
        WHERE d.Activo = 1";

    public async Task<IReadOnlyList<DeudorListado>> ListarAsync(
        string? filtro = null,
        bool soloConSaldo = false,
        int? limite = null,
        CancellationToken ct = default)
    {
        var seleccion = limite is > 0
            ? $"SELECT TOP({limite.Value}) d.IdDeudor"
            : "SELECT d.IdDeudor";

        var sql = SqlListadoBase.Replace("SELECT d.IdDeudor", seleccion) + @"
              AND (@filtro = ''
                   OR d.NombreCliente LIKE @like
                   OR ISNULL(d.Identificacion, '') LIKE @like
                   OR ISNULL(d.Telefono, '') LIKE @like)
              AND (@soloConSaldo = 0 OR ISNULL(m.Saldo, 0) > 0)
            ORDER BY ISNULL(m.Saldo, 0) DESC, d.NombreCliente";

        var texto = (filtro ?? string.Empty).Trim();

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@filtro", texto));
        cmd.Parameters.Add(new SqlParameter("@like", "%" + texto + "%"));
        cmd.Parameters.Add(new SqlParameter("@soloConSaldo", soloConSaldo ? 1 : 0));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<DeudorListado>();
        while (await reader.ReadAsync(ct)) lista.Add(LeerDeudor(reader));
        return lista;
    }

    public async Task<DeudorListado?> ObtenerAsync(int idDeudor, CancellationToken ct = default)
    {
        var sql = SqlListadoBase + " AND d.IdDeudor = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idDeudor));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? LeerDeudor(reader) : null;
    }

    public async Task<decimal> ObtenerSaldoAsync(int idDeudor, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ISNULL(SUM(CASE WHEN Tipo = 'C' THEN Valor ELSE -Valor END), 0)
            FROM DeudorMovimiento
            WHERE IdDeudor = @id AND Anulado = 0";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idDeudor));

        var res = await cmd.ExecuteScalarAsync(ct);
        return res is null || res == DBNull.Value ? 0m : Convert.ToDecimal(res);
    }

    public async Task<bool> ExisteIdentificacionAsync(
        string identificacion, int? idExcluir = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(identificacion)) return false;

        var sql = "SELECT COUNT(*) FROM Deudor WHERE Identificacion = @identificacion";
        if (idExcluir is not null) sql += " AND IdDeudor <> @excluir";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@identificacion", identificacion.Trim()));
        if (idExcluir is not null) cmd.Parameters.Add(new SqlParameter("@excluir", idExcluir.Value));

        var res = await cmd.ExecuteScalarAsync(ct);
        return res is not null && res != DBNull.Value && Convert.ToInt32(res) > 0;
    }

    // ---------- Alta y edicion ----------

    public async Task<int> CrearAsync(
        Deudor deudor,
        decimal valorDeudaInicial,
        string? descripcionDeuda,
        string? usuario,
        CancellationToken ct = default)
    {
        const string sqlInsert = @"
            INSERT INTO Deudor (NombreCliente, Identificacion, Telefono, Direccion, CupoMaximo, Observaciones, Activo)
            VALUES (@nombre, @identificacion, @telefono, @direccion, @cupo, @observaciones, 1);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

        try
        {
            int idDeudor;
            await using (var cmd = new SqlCommand(sqlInsert, con, tx))
            {
                AgregarCamposDeudor(cmd, deudor);
                var id = await cmd.ExecuteScalarAsync(ct);
                idDeudor = id is null || id == DBNull.Value ? 0 : Convert.ToInt32(id);
            }

            if (valorDeudaInicial > 0)
                await InsertarMovimientoAsync(con, tx, idDeudor, TipoMovimientoDeudor.Cargo,
                    valorDeudaInicial, descripcionDeuda, usuario, ct);

            await tx.CommitAsync(ct);
            return idDeudor;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ActualizarAsync(
        Deudor deudor,
        decimal valorNuevaDeuda,
        string? descripcionDeuda,
        string? usuario,
        CancellationToken ct = default)
    {
        const string sqlUpdate = @"
            UPDATE Deudor
            SET NombreCliente = @nombre, Identificacion = @identificacion, Telefono = @telefono,
                Direccion = @direccion, CupoMaximo = @cupo, Observaciones = @observaciones,
                FechaModificacion = SYSDATETIME()
            WHERE IdDeudor = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);

        try
        {
            await using (var cmd = new SqlCommand(sqlUpdate, con, tx))
            {
                AgregarCamposDeudor(cmd, deudor);
                cmd.Parameters.Add(new SqlParameter("@id", deudor.IdDeudor));
                await cmd.ExecuteNonQueryAsync(ct);
            }

            if (valorNuevaDeuda > 0)
                await InsertarMovimientoAsync(con, tx, deudor.IdDeudor, TipoMovimientoDeudor.Cargo,
                    valorNuevaDeuda, descripcionDeuda, usuario, ct);

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DesactivarAsync(int idDeudor, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE Deudor SET Activo = 0, FechaModificacion = SYSDATETIME()
            WHERE IdDeudor = @id";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idDeudor));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------- Movimientos ----------

    public async Task<IReadOnlyList<MovimientoDeudor>> ListarMovimientosAsync(
        int idDeudor,
        DateTime desde,
        DateTime hasta,
        bool verAnulados = false,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT m.IdMovimiento, m.Fecha, m.Tipo, m.Valor, m.Descripcion,
                   m.IdVenta, m.Usuario, m.Anulado
            FROM DeudorMovimiento m
            WHERE m.IdDeudor = @id
              AND m.Fecha >= @desde
              AND m.Fecha < DATEADD(DAY, 1, @hasta)
              AND (@verAnulados = 1 OR m.Anulado = 0)
            ORDER BY m.Fecha DESC, m.IdMovimiento DESC";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idDeudor));
        cmd.Parameters.Add(new SqlParameter("@desde", desde.Date));
        cmd.Parameters.Add(new SqlParameter("@hasta", hasta.Date));
        cmd.Parameters.Add(new SqlParameter("@verAnulados", verAnulados ? 1 : 0));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<MovimientoDeudor>();
        while (await reader.ReadAsync(ct))
        {
            lista.Add(new MovimientoDeudor(
                IdMovimiento: reader.GetInt32(0),
                Fecha: reader.GetDateTime(1),
                Tipo: reader.GetString(2).Trim(),
                Valor: reader.GetDecimal(3),
                Descripcion: reader.IsDBNull(4) ? null : reader.GetString(4),
                IdVenta: reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Usuario: reader.IsDBNull(6) ? null : reader.GetString(6),
                Anulado: reader.GetBoolean(7)));
        }
        return lista;
    }

    public async Task RegistrarAbonoAsync(
        int idDeudor,
        decimal valor,
        string? descripcion,
        string? usuario,
        CancellationToken ct = default)
    {
        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await InsertarMovimientoAsync(con, null, idDeudor, TipoMovimientoDeudor.Abono,
            valor, descripcion, usuario, ct);
    }

    public async Task AnularMovimientoAsync(int idMovimiento, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE DeudorMovimiento SET Anulado = 1
            WHERE IdMovimiento = @id AND Anulado = 0";

        await using var con = _factory.Crear();
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@id", idMovimiento));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------- Helpers ----------

    // Compartido con VentasRepository para la venta fiada, por eso es internal
    // y recibe la conexion y la transaccion del llamador.
    internal static async Task InsertarMovimientoAsync(
        SqlConnection con,
        SqlTransaction? tx,
        int idDeudor,
        string tipo,
        decimal valor,
        string? descripcion,
        string? usuario,
        CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO DeudorMovimiento (IdDeudor, Tipo, Valor, Descripcion, Usuario)
            VALUES (@idDeudor, @tipo, @valor, @descripcion, @usuario)";

        await using var cmd = tx is null
            ? new SqlCommand(sql, con)
            : new SqlCommand(sql, con, tx);

        cmd.Parameters.Add(new SqlParameter("@idDeudor", idDeudor));
        cmd.Parameters.Add(new SqlParameter("@tipo", tipo));
        cmd.Parameters.Add(new SqlParameter("@valor", valor));
        cmd.Parameters.Add(new SqlParameter("@descripcion",
            string.IsNullOrWhiteSpace(descripcion) ? DBNull.Value : descripcion.Trim()));
        cmd.Parameters.Add(new SqlParameter("@usuario",
            string.IsNullOrWhiteSpace(usuario) ? DBNull.Value : usuario));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AgregarCamposDeudor(SqlCommand cmd, Deudor d)
    {
        // Los opcionales van como NULL para no chocar con el indice unico
        // filtrado de Identificacion (varias cadenas vacias colisionarian).
        cmd.Parameters.Add(new SqlParameter("@nombre", d.NombreCliente));
        cmd.Parameters.Add(new SqlParameter("@identificacion", Opcional(d.Identificacion)));
        cmd.Parameters.Add(new SqlParameter("@telefono", Opcional(d.Telefono)));
        cmd.Parameters.Add(new SqlParameter("@direccion", Opcional(d.Direccion)));
        cmd.Parameters.Add(new SqlParameter("@cupo", d.CupoMaximo is null ? DBNull.Value : d.CupoMaximo.Value));
        cmd.Parameters.Add(new SqlParameter("@observaciones", Opcional(d.Observaciones)));
    }

    private static object Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

    private static DeudorListado LeerDeudor(SqlDataReader r) => new(
        IdDeudor: r.GetInt32(0),
        NombreCliente: r.GetString(1),
        Identificacion: r.IsDBNull(2) ? null : r.GetString(2),
        Telefono: r.IsDBNull(3) ? null : r.GetString(3),
        Direccion: r.IsDBNull(4) ? null : r.GetString(4),
        CupoMaximo: r.IsDBNull(5) ? null : r.GetDecimal(5),
        Saldo: r.GetDecimal(6),
        Observaciones: r.IsDBNull(7) ? null : r.GetString(7),
        FechaCreacion: r.GetDateTime(8),
        FechaModificacion: r.IsDBNull(9) ? null : r.GetDateTime(9));
}
