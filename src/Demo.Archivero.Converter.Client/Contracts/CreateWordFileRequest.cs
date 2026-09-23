namespace Demo.Archivero.Converter.Client.Contracts;

public sealed record CreateWordFileRequest(
    int FileId,
    string Username);