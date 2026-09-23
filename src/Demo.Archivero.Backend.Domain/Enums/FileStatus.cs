namespace Demo.Archivero.Domain.Enums;

public enum FileStatus : short
{
    None = 0,
    InQueue = 1,
    Ready = 2,
    Obsolete = 3
}