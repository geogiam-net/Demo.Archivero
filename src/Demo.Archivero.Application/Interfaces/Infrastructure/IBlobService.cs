namespace Demo.Archivero.Application.Interfaces.Infrastructure;

public interface IBlobService
{
    Task<string?> UploadToBlobAsync(
        int userId,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetTemporaryUrlsAsync(
        int userId,
        IEnumerable<string> blobNames,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteBlobAsync(
        int userId,
        string blobName,
        CancellationToken cancellationToken = default);
}
