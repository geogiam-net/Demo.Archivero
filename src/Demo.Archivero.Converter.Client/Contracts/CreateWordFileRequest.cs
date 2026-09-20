namespace Demo.Archivero.Converter.Client.Contracts;

public sealed record CreateWordFileRequest(
    string Username,
    string Title,
    string Content);
