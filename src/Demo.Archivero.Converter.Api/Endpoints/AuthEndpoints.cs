using Slg.DeadKm.Application.Dtos.Auth;
using Slg.DeadKm.Application.Interfaces.Infrastructure;
using Slg.DeadKm.Application.Security;
using System.Security.Claims;

namespace Slg.DeadKm.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Routes.Health, GetHealth);

        app.MapPost(Routes.Login, Login);

        app.MapGet(Routes.Me, GetMe).RequireAuthorization();

        return app;
    }

    private static IResult GetHealth()
    {
        return Results.Ok(new
        {
            status = "ok",
            service = "DeadKm API"
        });
    }

    private static async Task<IResult> Login(
        LoginRequestDto request,
        IAuthService auth,
        CancellationToken ct)
    {
        var result = await auth.LoginAsync(
            request.Username,
            request.Password,
            ct);

        return result is not null
            ? Results.Ok(result)
            : Results.Unauthorized();
    }

    private static IResult GetMe(ClaimsPrincipal user)
    {
        return Results.Ok(new CurrentUserDto(
            user.Identity?.Name ?? "unknown",
            user.FindFirstValue(ClaimTypes.Role) ?? AppRoles.Viewer,
            AppPermissionCatalog.GetPermissions(
                user.FindFirstValue(ClaimTypes.Role) ?? AppRoles.Viewer)));
    }
}
