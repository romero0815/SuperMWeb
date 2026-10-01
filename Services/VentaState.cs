using System.Text.Json;
using Microsoft.JSInterop;
using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Services;

// Estado de la venta en curso y de la venta encolada. Scoped: una instancia
// por circuito Blazor. Persiste en sessionStorage para sobrevivir a un F5.
public sealed class VentaState
{
    private const string StorageKey = "supermweb.venta";
    private readonly List<LineaTicket> _items = new();
    private readonly List<LineaTicket> _itemsEnCola = new();

    private sealed class Snapshot
    {
        public List<LineaTicket> Items { get; set; } = new();
        public List<LineaTicket> Cola { get; set; } = new();
    }

    public event Action? OnCambio;

    public IReadOnlyList<LineaTicket> Items => _items;
    public IReadOnlyList<LineaTicket> ItemsEnCola => _itemsEnCola;
    public bool HayVentaEnCola => _itemsEnCola.Count > 0;
    public decimal Total => VentaCalculadora.CalcularTotal(_items);
    public decimal TotalEnCola => VentaCalculadora.CalcularTotal(_itemsEnCola);
    public decimal DineroRecibido { get; private set; }
    public decimal Cambio => VentaCalculadora.CalcularCambio(DineroRecibido, Total);

    // Unidades de un producto que ya estan comprometidas en la venta encolada.
    public int CantidadEnCola(int idProducto) =>
        _itemsEnCola.Where(l => l.IdProducto == idProducto).Sum(l => l.Cantidad);

    public void AgregarLinea(LineaTicket linea)
    {
        ArgumentNullException.ThrowIfNull(linea);
        _items.Add(linea);
        OnCambio?.Invoke();
    }

    public void EliminarLinea(int indice)
    {
        if (indice < 0 || indice >= _items.Count) return;
        _items.RemoveAt(indice);
        OnCambio?.Invoke();
    }

    public void CambiarCantidad(int indice, int nuevaCantidad)
    {
        if (indice < 0 || indice >= _items.Count) return;
        if (nuevaCantidad <= 0) { EliminarLinea(indice); return; }
        var l = _items[indice];
        _items[indice] = new LineaTicket
        {
            IdProducto = l.IdProducto,
            Nombre = l.Nombre,
            PrecioUnitario = l.PrecioUnitario,
            Cantidad = nuevaCantidad
        };
        OnCambio?.Invoke();
    }

    public void SetDineroRecibido(decimal recibido)
    {
        DineroRecibido = recibido < 0 ? 0 : recibido;
        OnCambio?.Invoke();
    }

    // Limpia solo la venta actual; la cola no se toca.
    public void Reiniciar()
    {
        _items.Clear();
        DineroRecibido = 0m;
        OnCambio?.Invoke();
    }

    // Mueve la venta actual a la cola. Solo una venta encolada a la vez.
    public bool Encolar()
    {
        if (_items.Count == 0 || _itemsEnCola.Count > 0) return false;
        _itemsEnCola.AddRange(_items);
        _items.Clear();
        DineroRecibido = 0m;
        OnCambio?.Invoke();
        return true;
    }

    // Intercambia actual <-> encolada. Si la actual esta vacia, simplemente
    // retoma la encolada.
    public void IntercambiarConCola()
    {
        if (_itemsEnCola.Count == 0) return;
        var actual = _items.ToList();
        _items.Clear();
        _items.AddRange(_itemsEnCola);
        _itemsEnCola.Clear();
        _itemsEnCola.AddRange(actual);
        DineroRecibido = 0m;
        OnCambio?.Invoke();
    }

    // ---------- Persistencia sessionStorage ----------

    public async Task RestaurarDesdeStorageAsync(IJSRuntime js)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json)) return;

            var snap = JsonSerializer.Deserialize<Snapshot>(json);
            if (snap is null) return;

            _items.Clear();
            _items.AddRange(snap.Items);
            _itemsEnCola.Clear();
            _itemsEnCola.AddRange(snap.Cola);
            OnCambio?.Invoke();
        }
        catch
        {
            // Storage corrupto o inaccesible: ignorar, empezar en blanco.
        }
    }

    // Guarda actual + cola. Si ambas estan vacias, borra la clave.
    public async Task PersistirAsync(IJSRuntime js)
    {
        try
        {
            if (_items.Count == 0 && _itemsEnCola.Count == 0)
            {
                await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
                return;
            }
            var json = JsonSerializer.Serialize(new Snapshot
            {
                Items = _items.ToList(),
                Cola = _itemsEnCola.ToList()
            });
            await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);
        }
        catch { /* storage lleno o bloqueado: no critico */ }
    }
}