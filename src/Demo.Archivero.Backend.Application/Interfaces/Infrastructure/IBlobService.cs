namespace Demo.Archivero.Application.Interfaces.Infrastructure;

public interface IBlobService
{
    Task UploadToBlobAsync(string blobName, Stream data);
}