using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Serilog;
using SuperMWeb.Api;
using SuperMWeb.Application;
using SuperMWeb.Components;
using SuperMWeb.Infrastructure.Data;
using SuperMWeb.Infrastructure.Excel;
using SuperMWeb.Infrastructure.Printers;

// Serilog leido desde appsettings.json (Serilog seccion).
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Cuando corre como servicio Windows, el cwd es System32 (heredado del SCM);
    // fijamos ContentRoot al directorio del binario para que encuentre
    // appsettings.json y demas archivos.
    ContentRootPath = AppContext.BaseDirectory
});

// Habilita el hosting como servicio Windows. Solo tiene efecto cuando el
// proceso arranca via SCM (sc start); si no, es un no-op.
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "SuperMWeb";
});

builder.Host.UseSerilog((context, cfg) => cfg.ReadFrom.Configuration(context.Configuration));

// Cultura fija es-CO. La caja opera en Colombia; el parser de moneda del
// dominio y el formato del ticket dependen de esta cultura (ver §Cultura
// del inventario y decisiones 4 del prompt).
var culturaColombia = new CultureInfo("es-CO");
CultureInfo.DefaultThreadCurrentCulture = culturaColombia;
CultureInfo.DefaultThreadCurrentUICulture = culturaColombia;

// ---------- Servicios de infraestructura ----------

// SqlConnectionFactory como singleton: es thread-safe (solo mantiene la
// cadena) y crea SqlConnection nuevas por cada Crear().
var connectionString = builder.Configuration.GetConnectionString("SuperMDB")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:SuperMDB en appsettings.json.");
builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));

builder.Services.AddScoped<IProductosRepository, ProductosRepository>();
builder.Services.AddScoped<IVentasRepository, VentasRepository>();
builder.Services.AddScoped<IUsuariosRepository, UsuariosRepository>();
builder.Services.AddScoped<IReportesRepository, ReportesRepository>();
builder.Services.AddScoped<IProveedoresRepository, ProveedoresRepository>();
builder.Services.AddScoped<IProductosExcel, ProductosExcel>();

// Estado de venta, scanner y helpers de UI: Scoped por circuito Blazor.
builder.Services.AddScoped<SuperMWeb.Services.VentaState>();
builder.Services.AddScoped<SuperMWeb.Services.ScannerService>();
builder.Services.AddScoped<SuperMWeb.Services.UiInterop>();

// ITicketPrinter: seleccionable por config Printer:Tipo (Local o Null).
// Null se usa en Development sin hardware y en tests HTTP.
var printerTipo = builder.Configuration["Printer:Tipo"] ?? "Local";
if (string.Equals(printerTipo, "Null", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ITicketPrinter, NullTicketPrinter>();
}
else if (OperatingSystem.IsWindows())
{
    var nombreParcial = builder.Configuration["Printer:NombreParcial"] ?? "POS";
#pragma warning disable CA1416 // guard con OperatingSystem.IsWindows() arriba
    builder.Services.AddSingleton<ITicketPrinter>(_ => new LocalSpoolerPrinter(nombreParcial));
#pragma warning restore CA1416
}
else
{
    throw new PlatformNotSupportedException(
        "Printer:Tipo=Local solo esta soportado en Windows. Use 'Null' en otras plataformas.");
}

// ---------- Autenticacion (cookie) ----------

// Cookie de sesion contra la tabla Usuarios. Vive mientras dure el navegador
// o hasta cerrar sesion; sin sliding expiration porque la caja no cierra
// dentro de una jornada.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.Name = "supermweb.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor(); // requerido por Login.razor para generar token antiforgery en SSR

// ---------- Blazor Server ----------

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ---------- API cross-cutting ----------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sql-server");

var app = builder.Build();

// ---------- Pipeline ----------

// Cultura del request tambien fija en es-CO. Cubre model binding, formateo, etc.
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("es-CO")
    .AddSupportedCultures("es-CO")
    .AddSupportedUICultures("es-CO");
app.UseRequestLocalization(localizationOptions);

app.UseSerilogRequestLogging();

// Manejo de errores: en Prod emitimos ProblemDetails (mensaje humano, no
// stack trace) para todas las rutas. En Dev tambien via ProblemDetails,
// pero con detalle extendido gracias a AddProblemDetails + IncludeDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

// No hay HTTPS: mismo host, contexto seguro por localhost (prompt).
// app.UseHttpsRedirection() eliminado a proposito.

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapVentaEndpoints();
app.MapProductosEndpoints();
app.MapAuthEndpoints();
app.MapHealthChecks("/health");

app.Run();

// Necesario para tests HTTP (WebApplicationFactory<Program>).
public partial class Program { }
