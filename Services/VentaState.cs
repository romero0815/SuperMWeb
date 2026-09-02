using System.Text.Json;
using Microsoft.JSInterop;
using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Services;

// Estado de la venta en curso. Scoped: una instancia por circuito Blazor.
// Persiste en sessionStorage para sobrevivir a un F5 accidental del navegador
// (requisito del prompt seccion Fase 3).
public sealed class VentaState
{
    private const string StorageKey = "supermweb.venta.items";
    private readonly List<LineaTicket> _items = new();

    public event Action? OnCambio;

    public IReadOnlyList<LineaTicket> Items => _items;
    public decimal Total => VentaCalculadora.CalcularTotal(_items);
    public decimal DineroRecibido { get; private set; }
    public decimal Cambio => VentaCalculadora.CalcularCambio(DineroRecibido, Total);

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

    public void Reiniciar()
    {
        _items.Clear();
        DineroRecibido = 0m;
        OnCambio?.Invoke();
    }

    // ---------- Persistencia sessionStorage ----------

    // Restaura desde sessionStorage. Se llama en OnAfterRenderAsync(true)
    // del componente Venta, cuando JSRuntime ya esta disponible.
    public async Task RestaurarDesdeStorageAsync(IJSRuntime js)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json)) return;

            var restored = JsonSerializer.Deserialize<List<LineaTicket>>(json);
            if (restored is null) return;

            _items.Clear();
            _items.AddRange(restored);
            OnCambio?.Invoke();
        }
        catch
        {
            // Storage corrupto o inaccesible: ignorar, empezar en blanco.
        }
    }

    public async Task PersistirAsync(IJSRuntime js)
    {
        try
        {
            var json = JsonSerializer.Serialize(_items);
            await js.InvokeVoidAsync("sessionStorage.setItem", StorageKey, json);
        }
        catch { /* storage lleno o bloqueado: no critico */ }
    }

    public async Task LimpiarStorageAsync(IJSRuntime js)
    {
        try { await js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey); }
        catch { }
    }
}
