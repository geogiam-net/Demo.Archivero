using Azure.Storage.Blobs;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Demo.Archivero.Backend.Infrastructure.AzureBlob;

public class BlobService(
        ILogger<BlobService> logger,
        IConfiguration configuration
    ) : IBlobService
{
    public async Task UploadToBlobAsync(
        string blobName,
        Stream content)
    {
        var settings = configuration
            .GetSection(BlobServiceSettings.BlobServiceConfiguration)
            .Get<BlobServiceSettings>();

        var blobServiceClient = new BlobServiceClient(settings!.ConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(settings.ContainerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, overwrite: true);

        logger.LogInformation("File {BlobName} uploaded to Azure Blob Storage.", blobName);
    }
}