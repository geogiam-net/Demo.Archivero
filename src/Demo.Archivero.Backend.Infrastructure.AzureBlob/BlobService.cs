using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Demo.Archivero.Backend.Infrastructure.AzureBlob;

public sealed class BlobService(
    ILogger<BlobService> logger,
    IDateTimeProvider dateTimeProvider,
    IConfiguration configuration) : IBlobService
{
    public async Task<string?> UploadToBlobAsync(
        int userId,
        Stream content,
        CancellationToken ct)
    {
        if (userId <= 0 || content is null || !content.CanRead)
        {
            logger.LogWarning("Cannot upload a blob for user {UserId}: invalid user or stream.", userId);
            return null;
        }

        var blobId = Guid.NewGuid().ToString("N");

        try
        {
            var containerClient = await GetContainerClientAsync(ct);
            await GetUserBlobClient(containerClient, userId, blobId)
                .UploadAsync(content, overwrite: true, cancellationToken: ct);

            logger.LogInformation("Blob {BlobName} uploaded for user {UserId}.", blobId, userId);
            return blobId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload blob {BlobName} for user {UserId}.", blobId, userId);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> GetTemporaryUrlsAsync(
        int userId,
        IEnumerable<string> blobIds,
        TimeSpan expiresIn,
        CancellationToken ct)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);
        if (userId <= 0 || blobIds is null || expiresIn <= TimeSpan.Zero)
        {
            logger.LogWarning("Cannot create temporary blob URLs: invalid input for user {UserId}.", userId);
            return urls;
        }

        try
        {
            var containerClient = await GetContainerClientAsync(ct);
            foreach (var blobName in blobIds.Where(static name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.Ordinal))
            {
                var blobClient = GetUserBlobClient(containerClient, userId, blobName);
                if (!blobClient.CanGenerateSasUri)
                {
                    logger.LogError("Blob client cannot generate SAS URLs for user {UserId}.", userId);
                    return new Dictionary<string, string>(StringComparer.Ordinal);
                }

                var sas = new BlobSasBuilder
                {
                    BlobContainerName = containerClient.Name,
                    BlobName = blobClient.Name,
                    Resource = "b",
                    ExpiresOn = dateTimeProvider.UtcNowOffset.Add(expiresIn)
                };
                sas.SetPermissions(BlobSasPermissions.Read);
                urls[blobName] = blobClient.GenerateSasUri(sas).ToString();
            }

            return urls;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create temporary blob URLs for user {UserId}.", userId);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public async Task<bool> DeleteBlobAsync(
        int userId,
        string blobId,
        CancellationToken ct)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(blobId))
        {
            logger.LogWarning("Cannot delete a blob: invalid input for user {UserId}.", userId);
            return false;
        }

        try
        {
            var containerClient = await GetContainerClientAsync(ct);
            var response = await GetUserBlobClient(containerClient, userId, blobId).DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: ct);

            logger.LogInformation(
                "Blob {BlobName} deletion for user {UserId} completed. Deleted: {Deleted}",
                blobId,
                userId,
                response.Value);
            return response.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete blob {BlobName} for user {UserId}.", blobId, userId);
            return false;
        }
    }

    private async Task<BlobContainerClient> GetContainerClientAsync(CancellationToken cancellationToken)
    {
        var settings = configuration
            .GetSection(BlobServiceSettings.BlobServiceConfiguration)
            .Get<BlobServiceSettings>()
            ?? throw new InvalidOperationException($"Missing {BlobServiceSettings.BlobServiceConfiguration} configuration.");

        var serviceClient = new BlobServiceClient(settings.ConnectionString);

        var containerName = settings.ContainerName.ToLowerInvariant();
        var containerClient = serviceClient.GetBlobContainerClient(containerName);

        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return containerClient;
    }

    // Azure Blob Storage supports a single container level. Prefixing blob names models a per-user folder.
    private static BlobClient GetUserBlobClient(BlobContainerClient containerClient, int userId, string blobId)
        => containerClient.GetBlobClient($"{userId}/{blobId}");
}
