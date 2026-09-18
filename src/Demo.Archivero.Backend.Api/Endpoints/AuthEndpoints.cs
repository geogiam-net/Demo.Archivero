using Demo.Archivero.Application.Dtos.Auth;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Security;
using System.Security.Claims;

namespace Demo.Archivero.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Routes.Ping, GetHealth);
        app.MapPost(Routes.Login, Login);
        app.MapGet(Routes.Me, GetMe).RequireAuthorization();

        return app;
    }

    private static IResult GetHealth()
    {
        return Results.Ok(new
        {
            status = "ok",
            service = "Archivero API is up"
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

        return ResultDtoResultMapper.ToHttpResult(
            result,
            employee => TypedResults.Ok(result));
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
