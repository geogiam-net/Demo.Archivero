using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Dtos.File;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Application.Services;

public class FileService(
    IFileRepository fileRepository,
    IUserRepository userRepository,
    ILogger<FileService> logger,
    IDateTimeProvider dateTimeProvider,
    IOpenXmlWordService openXmlWordService,
    IBlobService blobService
    ) : IFileService
{
    public async Task<ResultDto<bool>> CreateFileAsync(string title, string content, string username, CancellationToken ct)
    {
        var user = await userRepository.GetUserAsync(username, ct);

        if (user is null)
        {
            return new ResultDto<bool>(false, Domain.Enums.Error.NotFound, new[] { "User not found." });
        }

        // Demo.Archivero.Backend.Api only sends data to queue for creation by another server
        logger.LogInformation("File set for creation by user: {Username} at {CreatedAt}", username, dateTimeProvider.UtcNow);

        // ########################################################################################

        // second server creates file, blob, then saves entry into database
        using (var stream = openXmlWordService.CreateDocument(title, content)) 
        {
            var blobId = await blobService.UploadToBlobAsync(user.Id, stream, ct);
            if (blobId is null) 
            {
                return new ResultDto<bool>(false, Domain.Enums.Error.InternalServerError, new[] { "Failed to upload blob." });
            }

            FileEntity newFile = new FileEntity
            {
                Title = title,
                BlobId = blobId,
                OwnerId = user.Id,
                Status = Domain.Enums.FileStatus.Available
            };

            var id = await fileRepository.CreateFileAsync(newFile, user, ct);

            logger.LogInformation("File created: {FileId} by user: {Username} at {CreatedAt}", id, username, dateTimeProvider.UtcNow);
        }

        return new ResultDto<bool>(true);
    }

    public async Task<ResultDto<IReadOnlyList<FileDto>>> GetFilesAsync(string username, CancellationToken ct)
    {
        var user = await userRepository.GetUserAsync(username, ct);

        if (user is null)
        {
            return new ResultDto<IReadOnlyList<FileDto>>(new List<FileDto>(), Domain.Enums.Error.NotFound, new[] { "User not found." });
        }

        var files = await fileRepository.GetFilesAsync(user.Id, ct);

        var urls = await blobService.GetTemporaryUrlsAsync(user.Id, files.Select(f => f.BlobId).ToList(), TimeSpan.FromMinutes(15), ct);

        var fileDtos = files.Select(f => new FileDto(
            f.Title,
            f.CreatedAtUtc,
            urls.TryGetValue(f.BlobId, out var url) ? url : ""
        )).ToList();

        return new ResultDto<IReadOnlyList<FileDto>>(fileDtos);
    }

    public async Task<ResultDto<bool>> DeleteFileAsync(int fileId, string username, CancellationToken ct)
    {
        var user = await userRepository.GetUserAsync(username, ct);

        if (user is null)
        {
            return new ResultDto<bool>(false, Domain.Enums.Error.NotFound, new[] { "User not found." });
        }

        // Demo.Archivero.Backend.Api only set it to obsolete, then it is sent to queue for deletion by another server
        var result =  await fileRepository.SetFileAsObsoleteAsync(fileId, user, ct);

        if (result.Result)
        {
            logger.LogInformation("File marked for deletion: {FileId} by user: {Username} at {DeletedAt}", fileId, username, dateTimeProvider.UtcNow);
        }

        // ########################################################################################

        // second server deletes blob, then file entry in database

        var file = await fileRepository.GetFileAsync(fileId, ct);

        if (file is null)
        {
            return new ResultDto<bool>(
                false,
                Domain.Enums.Error.NotFound,
                new List<string> { "File not found." }
            );
        }

        await blobService.DeleteBlobAsync(user.Id, file.BlobId, ct);

        await fileRepository.DeleteFileAsync(fileId, ct);

        if (result.Result)
        {
            logger.LogInformation("File deleted: {FileId} by user: {Username} at {DeletedAt}", fileId, username, dateTimeProvider.UtcNow);
        }

        return result;
    }

}