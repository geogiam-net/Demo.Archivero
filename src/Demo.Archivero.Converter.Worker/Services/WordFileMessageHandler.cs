using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Domain.Enums;
using Demo.Archivero.Infrastructure.ServiceBus;

namespace Demo.Archivero.Converter.Worker.Services;

public sealed class WordFileMessageHandler(IWordFileService fileService, ILogger<WordFileMessageHandler> logger)
    : IWordFileMessageHandler
{
    public async Task<bool> CreateWordFileAsync(WordFileMessage message, string messageId, CancellationToken ct)
        => Succeeded(await fileService.CreateWordFileAsync(message.FileId, message.Username, ct), messageId);

    public async Task<bool> DeleteWordFileAsync(WordFileMessage message, string messageId, CancellationToken ct)
        => Succeeded(await fileService.DeleteWordFileAsync(message.FileId, message.Username, ct), messageId);

    private bool Succeeded(ResultDto<bool> result, string messageId)
    {
        if (result.Result && result.ErrorCode == Error.None) return true;
        logger.LogWarning("Word file message {MessageId} failed with {ErrorCode}: {Errors}",
            messageId, result.ErrorCode, string.Join("; ", result.ErrorMessages ?? []));
        return false;
    }
}
