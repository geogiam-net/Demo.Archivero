using Demo.Archivero.Infrastructure.ServiceBus;

namespace Demo.Archivero.Converter.Worker.Workers;

// The processor continuously receives available messages and renews PeekLock locks.
public sealed class CreateWordFileWorker(CreateWordFileReceiver receiver) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => receiver.StartAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => receiver.StopAsync(cancellationToken);
}

