using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Dtos.File;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Backend.Application.Settings;
using Demo.Archivero.Converter.Client;
using Demo.Archivero.Converter.Client.Contracts;
using Microsoft.Extensions.Logging;
using System.Net;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.Application.Services;

public class FileService(
    IFileRepository fileRepository,
    IUserRepository userRepository,
    ILogger<FileService> logger,
    IDateTimeProvider dateTimeProvider,
    IBlobService blobService,
    IConverterApiClient converterApiClient
    ) : IFileService
{
    public async Task<ResultDto<bool>> QueueFileAsync(string title, string content, string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new ResultDto<bool>(
                false,
                Domain.Enums.Error.ValidationError,
                new[] { "File title is required." });
        }

        title = title.Trim();
        if (title.Length > EntitiesSettings.FileTitleMaxLength)
        {
            return new ResultDto<bool>(
                false,
                Domain.Enums.Error.ValidationError,
                new[] { $"File title cannot exceed {EntitiesSettings.FileTitleMaxLength} characters." });
        }

        if (content?.Length > EntitiesSettings.FileContentMaxLength)
        {
            return new ResultDto<bool>(
                false,
                Domain.Enums.Error.ValidationError,
                new[] { $"File content cannot exceed {EntitiesSettings.FileContentMaxLength} characters." });
        }

        var user = await userRepository.GetUserAsync(username, ct);

        if (user is null)
        {
            return new ResultDto<bool>(false, Domain.Enums.Error.NotFound, new[] { "User not found." });
        }

        logger.LogInformation("File set for creation by user: {Username} at {CreatedAt}", username, dateTimeProvider.UtcNow);


        // EF Core persists this value as a parameter; do not SQL-escape user text.
        FileEntity newFile = new FileEntity
        {
            Title = title,
            Content = content ?? "",
            BlobId = string.Empty,
            OwnerId = user.Id,
            Status = Domain.Enums.FileStatus.InQueue
        };

        var idResult = await fileRepository.SetInQueueFileAsync(newFile, user, ct);

        // This will be an Azure Queue call later
        await converterApiClient.CreateWordFileAsync(
            new CreateWordFileRequest(idResult.Result, username),
            ct);

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
            f.Id,
            WebUtility.HtmlEncode(f.Title),
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

        var result =  await fileRepository.SetFileAsObsoleteAsync(fileId, user, ct);

        if (result.Result)
        {
            logger.LogInformation("File marked for deletion: {FileId} by user: {Username} at {DeletedAt}", fileId, username, dateTimeProvider.UtcNow);
        }

        // This will be an Azure Queue call later
        await converterApiClient.DeleteWordFileAsync(fileId, username, ct);

        return result;
    }
}
