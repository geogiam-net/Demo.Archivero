namespace Demo.Archivero.Application.Dtos;

// This generic object is used to return results from deep parts of the Application, for example from User Cases or the Domain.
// we avoid throwing exceptions because they are costly in resources and are complicated to control
public sealed record ResultDto<T>(
    T Result,
    IReadOnlyList<int> ErrorCode,
    IReadOnlyList<string> ErrorMessages
    );
