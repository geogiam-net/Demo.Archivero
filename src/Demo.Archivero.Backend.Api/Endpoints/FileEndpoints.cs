using Demo.Archivero.Application.Dtos.File;
using Demo.Archivero.Application.Interfaces.Application;
using DocumentFormat.OpenXml.Spreadsheet;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Security.Claims;

namespace Demo.Archivero.Api.Endpoints;

public static class FileEndpoints   
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Routes.CreateFile, CreateFile).RequireAuthorization();
        app.MapGet(Routes.GetFiles, GetFiles).RequireAuthorization();
        app.MapDelete(Routes.DeleteFile, DeleteFile).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> CreateFile(CreateFileRequestDto request, ClaimsPrincipal user, IFileService fileService, CancellationToken ct)
    {
        var result = await fileService.QueueFileAsync(request.Title, request.Content, user.Identity?.Name ?? string.Empty, ct);

        return ResultDtoResultMapper.ToHttpResult(
            result,
            _ => TypedResults.Ok());
    }

    private static async Task<IResult> GetFiles(ClaimsPrincipal user, IFileService fileService, IRedisDatabase redis, CancellationToken ct)
    {
        var cachedFileDtoList = await redis.GetAsync<IReadOnlyList<FileDto>>(Routes.GetFiles);
        if(cachedFileDtoList != null)
        {
            if (!cachedFileDtoList.Any())
            {
                return Results.NotFound();
            }

            return TypedResults.Ok(cachedFileDtoList);
        }

        var result = await fileService.GetFilesAsync(user.Identity?.Name ?? string.Empty, ct);

        if (result.Result == null || !result.Result.Any())
        {
            await redis.AddAsync(Routes.GetFiles, Array.Empty<FileDto>(), TimeSpan.FromMinutes(1));
            return Results.NotFound();
        }

        if (result.ErrorCode == Domain.Enums.Error.None) 
        {
            await redis.AddAsync(Routes.GetFiles, result.Result, TimeSpan.FromSeconds(30));
        }

        return ResultDtoResultMapper.ToHttpResult(
            result,
            _ => TypedResults.Ok(result.Result));
    }

    private static async Task<IResult> DeleteFile(int fileId, ClaimsPrincipal user, IFileService fileService, IRedisDatabase redis, CancellationToken ct)
    {
        var result = await fileService.DeleteFileAsync(fileId, user.Identity?.Name ?? string.Empty, ct);

        if (result.ErrorCode == Domain.Enums.Error.None)
        {
            await redis.RemoveAsync(Routes.GetFiles);
        }

        return ResultDtoResultMapper.ToHttpResult(
            result,
            _ => TypedResults.Ok());
    }
}