using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Application;
using Microsoft.Extensions.Logging;

namespace Demo.Archivero.Application.Services;

public class FileService(
    ILogger<FileService> logger,
    IDateTimeProvider dateTimeProvider
    ) : IFileService
{
   
}