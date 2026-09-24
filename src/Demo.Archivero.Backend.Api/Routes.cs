namespace Demo.Archivero.Api;
public static class Routes
{
    public const string Health = "/api/health";

    // AuthEndpoints
    public const string Login = "/api/auth/login";
    public const string Me = "/api/auth/me";

    // FileEndpoints
    public const string CreateFile = "/api/files";
    public const string GetFiles = "/api/files";
    public const string DeleteFile = "/api/files/{fileId:int}";
}