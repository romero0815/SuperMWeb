using Microsoft.JSInterop;

namespace SuperMWeb.Services;

// Servicio scoped que envuelve el modulo JS wwwroot/js/scanner.js.
// El componente que quiera oir escaneos hace:
//   scanner.OnEscaneo += MiHandler;
//   await scanner.IniciarAsync();
// Y en OnDispose:
//   scanner.OnEscaneo -= MiHandler;
//   await scanner.DetenerAsync();
public sealed class ScannerService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly IConfiguration _config;
    private IJSObjectReference? _module;
    private DotNetObjectReference<ScannerService>? _selfRef;

    public event Action<string>? OnEscaneo;

    public ScannerService(IJSRuntime js, IConfiguration config)
    {
        _js = js;
        _config = config;
    }

    public async Task IniciarAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/scanner.js");
        _selfRef ??= DotNetObjectReference.Create(this);
        var sufijo = _config["Scanner:Sufijo"] ?? "Enter";
        var prefijo = _config["Scanner:Prefijo"] ?? string.Empty;
        await _module.InvokeVoidAsync("iniciar", _selfRef, sufijo, prefijo);
    }

    public async Task DetenerAsync()
    {
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("detener"); } catch { /* modulo ya descartado */ }
        }
    }

    [JSInvokable]
    public Task OnEscaneoJs(string codigo)
    {
        OnEscaneo?.Invoke(codigo);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DetenerAsync();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch { }
        }
        _selfRef?.Dispose();
    }
}
