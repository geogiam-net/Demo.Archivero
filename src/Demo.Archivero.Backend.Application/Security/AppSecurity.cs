using System.Collections.ObjectModel;

namespace Demo.Archivero.Application.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Viewer = "Viewer";
}

public static class AppPermissions
{
    public const string ArchiveroView = "Archivero.View";

    public static readonly IReadOnlySet<string> All = new ReadOnlySet<string>(
        new HashSet<string>
        {
            ArchiveroView
        });
}

public static class AppPermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [AppRoles.Admin] = AppPermissions.All,
            [AppRoles.Viewer] = new ReadOnlySet<string>(
                new HashSet<string>
                {
                    AppPermissions.ArchiveroView
                })
        };

    public static IReadOnlyList<string> GetPermissions(string? role)
    {
        var resolved = ResolveRole(role);
        if (resolved is null || !RolePermissions.TryGetValue(resolved, out var permissions))
        {
            return [];
        }

        return permissions.OrderBy(x => x).ToArray();
    }

    public static bool HasPermission(string? role, string permission)
    {
        var resolved = ResolveRole(role);
        return resolved is not null &&
            RolePermissions.TryGetValue(resolved, out var permissions) &&
            permissions.Contains(permission);
    }

    public static bool HasAnyPermission(string? role, params string[] permissions)
    {
        return permissions.Any(permission => HasPermission(role, permission));
    }

    public static bool IsSupportedRole(string role)
    {
        return ResolveRole(role) is not null;
    }

    public static string NormalizeRole(string role)
    {
        var normalized = role.Trim();
        var match = ResolveRole(normalized);

        if (match is null)
        {
            throw new InvalidOperationException(
                $"Role '{role}' is not supported.");
        }

        return match;
    }

    private static string? ResolveRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        var normalized = role.Trim();
        var match = RolePermissions.Keys.FirstOrDefault(
            x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        return null;
    }
}
