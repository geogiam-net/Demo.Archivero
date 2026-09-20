namespace Demo.Archivero.Converter.Api;
public static class Routes
{
    public const string Ping = "/api/ping";

    // FileEndpoints
    public const string CreateWordFile = "/api/wordfiles";
    public const string DeleteWordFile = "/api/wordfiles/{fileId:int}/user/{username}";
}