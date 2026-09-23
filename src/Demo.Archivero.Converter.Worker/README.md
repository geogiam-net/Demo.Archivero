# Converter worker

This executable uses the .NET generic host, with two independent hosted services:

- `CreateWordFileWorker` listens to `ArchiveroFileBus:CreateWordFileQueueName` and
  invokes `IWordFileService.CreateWordFileAsync(FileId, Username, ct)`.
- `DeleteWordFileWorker` listens to `ArchiveroFileBus:DeleteWordFileQueueName` and
  invokes `IWordFileService.DeleteWordFileAsync(FileId, Username, ct)`.

Each worker depends on its own receiver (`CreateWordFileReceiver` or
`DeleteWordFileReceiver`); initializing one creates only its own queue processor.
These replace the former HTTP file endpoints. There is no HTTP listener or Swagger.
The Service Bus processors wait for available messages continuously; no timer or
queue-count polling is needed. Each delivery gets its own dependency-injection
scope, including a separate database context. Shutdown stops both processors.

Configure `ConnectionStrings:ArchiveroDB`, `ArchiveroBlobService:ConnectionString`,
`ArchiveroBlobService:ContainerName`, and all three `ArchiveroFileBus` settings:
`ConnectionString`, `CreateWordFileQueueName`, and `DeleteWordFileQueueName`.
Use user secrets or environment variables (replace `:` with `__`).
Provision queues and apply database migrations before starting the worker.

Both receivers use PeekLock with explicit completion only when the application
service returns `Result == true` and `ErrorCode == None`. Failed results and
exceptions are retried; invalid messages and exhausted retries are retained in
dead-letter queues. Monitor and replay these queues after resolving failures.

The existing application workflow is not fully idempotent across blob/database
partial failures or redelivery. For example, an already-ready file returns a
conflict on a repeated create, and an already-deleted file returns not-found.
These are not acknowledged as successes and can reach the dead-letter queue.
Cross-queue ordering is not guaranteed. Durable idempotency and the producer's
transactional outbox remain necessary for end-to-end reliable processing.

Run with `dotnet run --project src/Demo.Archivero.Converter.Worker`.
