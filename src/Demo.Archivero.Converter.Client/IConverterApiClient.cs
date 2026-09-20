using Demo.Archivero.Converter.Client.Contracts;

namespace Demo.Archivero.Converter.Client;

public interface IConverterApiClient
{
    Task<ConverterPingResponse> PingAsync(CancellationToken cancellationToken = default);

    Task CreateWordFileAsync(
        CreateWordFileRequest request,
        CancellationToken ct);

    Task DeleteWordFileAsync(
        int fileId,
        string username,
        CancellationToken ct);
}
