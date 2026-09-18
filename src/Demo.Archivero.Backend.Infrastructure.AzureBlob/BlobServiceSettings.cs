namespace Demo.Archivero.Backend.Infrastructure.AzureBlob;

public class BlobServiceSettings
{
    public const string BlobServiceConfiguration = "ArchiveroBlobService";

    public required string ConnectionString { get; set; }

    public required string ContainerName { get; set; }
}