using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Domain.Enums;

namespace Demo.Archivero.Api;

internal static class ResultDtoResultMapper
{
    internal static IResult ToHttpResult<T>(
        ResultDto<T> result,
        Func<T, IResult> onSuccess)
    {
        return result.ErrorCode switch
        {
            Error.None => onSuccess(result.Result),
            Error.ValidationError => Results.BadRequest(result.ErrorMessages),
            Error.Conflict => Results.Conflict(result.ErrorMessages),
            Error.NotFound => Results.NotFound(result.ErrorMessages),
            Error.NotAuthorized => Results.Unauthorized(),
            _ => Results.InternalServerError(result.ErrorMessages)
        };
    }
}
