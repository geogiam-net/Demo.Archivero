using Microsoft.AspNetCore.Authorization;
using Demo.Archivero.Application.Security;
using System.Security.Claims;

namespace Demo.Archivero.Api.Security;

internal static class AuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequirePermission(
        this AuthorizationPolicyBuilder policy,
        string permission)
    {
        return policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                AppPermissionCatalog.HasPermission(
                    context.User.FindFirstValue(ClaimTypes.Role),
                    permission));
    }

    public static AuthorizationPolicyBuilder RequireAnyPermission(
        this AuthorizationPolicyBuilder policy,
        params string[] permissions)
    {
        return policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                AppPermissionCatalog.HasAnyPermission(
                    context.User.FindFirstValue(ClaimTypes.Role),
                    permissions));
    }
}
