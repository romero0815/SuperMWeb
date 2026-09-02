using Microsoft.JSInterop;

namespace SuperMWeb.Services;

// Envoltorio C# de wwwroot/js/ui.js (beep + focus).
public sealed class UiInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public UiInterop(IJSRuntime js) { _js = js; }

    private async Task<IJSObjectReference> ModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/ui.js");
        return _module;
    }

    public async Task BeepAsync(int frecuencia = 660, int ms = 150)
    {
        try
        {
            var m = await ModuleAsync();
            await m.InvokeVoidAsync("beep", frecuencia, ms);
        }
        catch { /* no critico */ }
    }

    public async Task EnfocarAsync(string selector)
    {
        try
        {
            var m = await ModuleAsync();
            await m.InvokeVoidAsync("enfocar", selector);
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch { }
        }
    }
}
