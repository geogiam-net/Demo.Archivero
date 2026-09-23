using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Backend.Application.Settings;
using Demo.Archivero.Domain.Enums;
using Microsoft.Extensions.Logging;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Application.Services;

public class WordFileService(
    IFileRepository fileRepository,
    IUserRepository userRepository,
    ILogger<WordFileService> logger,
    IDateTimeProvider dateTimeProvider,
    IOpenXmlWordService openXmlWordService,
    IBlobService blobService
    ) : IWordFileService
{
    public async Task<ResultDto<bool>> CreateWordFileAsync(int fileId, string username, CancellationToken ct)
    {
        var user = await userRepository.GetUserAsync(username, ct);

        if (user is null)
        {
            return new ResultDto<bool>(false, Domain.Enums.Error.NotFound, new[] { "User not found." });
        }

        var file = await fileRepository.GetFileAsync(fileId, ct);

        if (file is null)
        {
            return new ResultDto<bool>(false, Domain.Enums.Error.NotFound, new[] { "File not found." });
        }

        if(file.Status != FileStatus.InQueue)
        {
            return new ResultDto<bool>(false, Domain.Enums.Error.Conflict, new[] { "File is not in InQueue state." });
        }

        using (var stream = openXmlWordService.CreateDocument(file.Title, file.Content)) 
        {
            var blobId = await blobService.UploadToBlobAsync(user.Id, stream, ct);
            if (blobId is null) 
            {
                return new ResultDto<bool>(false, Domain.Enums.Error.InternalServerError, new[] { "Failed to upload blob." });
            }

            await fileRepository.ReadyFileAsync(file, user, ct);

            logger.LogInformation("Word file created: {FileId} by user: {Username} at {CreatedAt}", fileId, username, dateTimeProvider.UtcNow);
        }

        return new ResultDto<bool>(true);
    }

    public async Task<ResultDto<bool>> DeleteWordFileAsync(int fileId, string username, CancellationToken ct)
    {
        var user = await userRepository.GetUserAsync(username, ct);

        if (user is null)
        {
            return new ResultDto<bool>(false, Domain.Enums.Error.NotFound, new[] { "User not found." });
        }

        var file = await fileRepository.GetFileAsync(fileId, ct);

        if (file is null)
        {
            return new ResultDto<bool>(
                false,
                Domain.Enums.Error.NotFound,
                new List<string> { "File not found." }
            );
        }

        var result1 = await blobService.DeleteBlobAsync(user.Id, file.BlobId, ct);

        if(!result1)
        {
            logger.LogError("Failed to delete blob: {BlobId} for file: {FileId} by user: {Username} at {DeletedAt}", 
                file.BlobId, fileId, username, dateTimeProvider.UtcNow);

            return new ResultDto<bool>(
                false,
                Domain.Enums.Error.InternalServerError,
                new List<string> { "Failed to delete blob." }
            );
        }


        var result2 = await fileRepository.DeleteFileAsync(fileId, ct);

        if (result2.Result)
        {
            logger.LogInformation("File deleted: {FileId} by user: {Username} at {DeletedAt}", fileId, username, dateTimeProvider.UtcNow);
        }
        else
        {
            logger.LogError("Failed to delete file: {FileId} by user: {Username} at {DeletedAt}", fileId, username, dateTimeProvider.UtcNow);

            return new ResultDto<bool>(
                false,
                Domain.Enums.Error.InternalServerError,
                new List<string> { "Failed to delete file." }
            );
        }

        return result2;
    }

}
