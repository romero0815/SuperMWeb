using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SuperMWeb.Application;

namespace SuperMWeb.Api;

// Endpoints minimal API para login y logout con cookie auth.
// Se llaman desde el <form method="post"> del componente Login.razor
// (SSR, sin interactive). Cumplen con antiforgery estandar.
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/login con Usuario y Clave del form. Ruta bajo /api/ para
        // no colisionar con la pagina Blazor /login (@page "/login").
        // Redirige a returnUrl si viene, si no a /.
        app.MapPost("/api/login", async (
            HttpContext http,
            IUsuariosRepository usuarios,
            IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(http);

            var form = await http.Request.ReadFormAsync();
            var usuario = (form["Usuario"].ToString() ?? string.Empty).Trim();
            var clave = (form["Clave"].ToString() ?? string.Empty).Trim();
            var returnUrl = form["ReturnUrl"].ToString();

            //validamos usuario ingresado y recolectamos su informacion
            var userDB = await usuarios.ObtenerPorUsuarioAsync(usuario, http.RequestAborted);
            if(userDB is null)
            {
                var qs = string.IsNullOrEmpty(returnUrl)
                    ? "?error=2"
                    : $"?error=2&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.Redirect("/login" + qs);
            }

            //esto solo servira mientras se hace el procesos de migracion, una vez todos los usuarios ya tengan su clave actualizada
            //ya no entrara por el if
            var esHash = PasswordHelper.EsHashBCrypt(userDB.Clave);
            if(!esHash)
            {
                //actualizamos silenciosamente la clave del usuario a hash bcrypt
                userDB.Clave = PasswordHelper.HashPassword(userDB.Clave);
                await usuarios.ActualizarContraseñaAsync(userDB.IdUsuario, userDB.Clave, http.RequestAborted);
            }


            //validamos la clave ingresada con la clave encriptada en la base de datos
            var validatePassword = PasswordHelper.VerifyPassword(clave, userDB.Clave);
            if (!validatePassword)
            {
                var qs = string.IsNullOrEmpty(returnUrl)
                    ? "?error=1"
                    : $"?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.Redirect("/login" + qs);
            }

            //si todo esta bien, autenticamos al usuario y creamos la cookie de autenticacion
            var u = await usuarios.AutenticarAsync(userDB.UsuarioLogin, userDB.Clave, http.RequestAborted);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, u.IdUsuario.ToString()),
                new(ClaimTypes.Name, u.Usuario),
                new("nombre", u.Nombre),
                new(ClaimTypes.Role, u.Rol),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }).DisableAntiforgery(); // validamos manualmente arriba

        app.MapPost("/api/logout", async (HttpContext http, IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(http);
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }).DisableAntiforgery();

        return app;
    }
}
