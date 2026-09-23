using Demo.Archivero.Application.Dtos.File;
using Demo.Archivero.Application.Interfaces.Application;
using System.Security.Claims;

namespace Demo.Archivero.Converter.Api.Endpoints;

public static class FileEndpoints   
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Routes.CreateWordFile, CreateFile);
        app.MapDelete(Routes.DeleteWordFile, DeleteFile);

        return app;
    }

    private static async Task<IResult> CreateFile(CreateWordFileRequestDto request, ClaimsPrincipal user, IWordFileService fileService, CancellationToken ct)
    {
        var result = await fileService.CreateWordFileAsync(request.FileId, request.Username, ct);

        return ResultDtoResultMapper.ToHttpResult(
            result,
            _ => TypedResults.Ok());
    }
 
    private static async Task<IResult> DeleteFile(int fileId, string username, IWordFileService fileService, CancellationToken ct)
    {
        var result = await fileService.DeleteWordFileAsync(fileId, username, ct);

        return ResultDtoResultMapper.ToHttpResult(
            result,
            _ => TypedResults.Ok());
    }
}