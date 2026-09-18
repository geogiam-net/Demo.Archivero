namespace Demo.Archivero.Domain.Enums;

public enum Error
{
    None = 0,

    InternalServerError = 1,

    NotFound = 2,

    ParameterError = 3,

    MissingData = 4,

    ValidationError = 5,

    Conflict = 6,

    NotAuthorized = 7,
}