using SuperMWeb.Domain;

namespace SuperMWeb.Application;

// Modulo de fiado. Portado de DeudoresForm, DeudorMovimientoForm,
// FrmAgregarDeuda y FrmAgregarAbono del desktop.
//
// El saldo nunca se guarda: siempre se calcula como
// SUM(cargos) - SUM(abonos) sobre los movimientos no anulados.
public interface IDeudoresRepository
{
    // --- Consulta ---

    // filtro busca por nombre, identificacion o telefono. soloConSaldo deja
    // solo los que deben dinero. Ordena por saldo descendente y luego nombre.
    Task<IReadOnlyList<DeudorListado>> ListarAsync(
        string? filtro = null,
        bool soloConSaldo = false,
        int? limite = null,
        CancellationToken ct = default);

    Task<DeudorListado?> ObtenerAsync(int idDeudor, CancellationToken ct = default);

    // Saldo sobre todo el historico, sin filtro de fechas.
    Task<decimal> ObtenerSaldoAsync(int idDeudor, CancellationToken ct = default);

    // True si otro deudor ya usa esa identificacion (indice unico filtrado
    // UX_Deudor_Identificacion). idExcluir ignora al propio deudor al editar.
    Task<bool> ExisteIdentificacionAsync(
        string identificacion,
        int? idExcluir = null,
        CancellationToken ct = default);

    // --- Alta y edicion ---

    // Crea el deudor y, si valorDeudaInicial es mayor que cero, le registra
    // el cargo inicial en la misma transaccion. Devuelve el IdDeudor.
    Task<int> CrearAsync(
        Deudor deudor,
        decimal valorDeudaInicial,
        string? descripcionDeuda,
        string? usuario,
        CancellationToken ct = default);

    // Actualiza los datos del deudor y, si valorNuevaDeuda es mayor que cero,
    // le agrega ese cargo en la misma transaccion.
    Task ActualizarAsync(
        Deudor deudor,
        decimal valorNuevaDeuda,
        string? descripcionDeuda,
        string? usuario,
        CancellationToken ct = default);

    // Soft-delete: Activo = 0. El llamador debe comprobar antes que el saldo
    // sea cero; el desktop no deja inactivar a quien todavia debe.
    Task DesactivarAsync(int idDeudor, CancellationToken ct = default);

    // --- Movimientos ---

    // Rango [desde, hasta] inclusivo en ambos extremos: la consulta usa
    // Fecha < DATEADD(DAY, 1, hasta), igual que el desktop.
    Task<IReadOnlyList<MovimientoDeudor>> ListarMovimientosAsync(
        int idDeudor,
        DateTime desde,
        DateTime hasta,
        bool verAnulados = false,
        CancellationToken ct = default);

    Task RegistrarAbonoAsync(
        int idDeudor,
        decimal valor,
        string? descripcion,
        string? usuario,
        CancellationToken ct = default);

    // Marca Anulado = 1. No borra la fila y recalcula el saldo por diferencia.
    Task AnularMovimientoAsync(int idMovimiento, CancellationToken ct = default);
}
