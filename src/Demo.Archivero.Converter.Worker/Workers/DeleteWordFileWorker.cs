using Demo.Archivero.Infrastructure.ServiceBus;

namespace Demo.Archivero.Converter.Worker.Workers;

public sealed class DeleteWordFileWorker(DeleteWordFileReceiver receiver) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => receiver.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => receiver.StopAsync(cancellationToken);
}

