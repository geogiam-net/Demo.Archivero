namespace Demo.Archivero.Application.Interfaces.Infrastructure;

public interface IOpenXmlWordService
{
    MemoryStream CreateDocument(string title, string content);
}