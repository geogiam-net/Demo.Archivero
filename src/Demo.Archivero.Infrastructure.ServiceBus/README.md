# Word file Service Bus transport

This project provides independent `CreateWordFileSender`, `DeleteWordFileSender`,
`CreateWordFileReceiver`, and `DeleteWordFileReceiver` classes, `FileBusSettings`, and a
`WordFileMessage(FileId, Username)` contract. Create and delete use separate queues
and subjects (`CreateWordFile` and `DeleteWordFile`). The backend API registers the
sender and `FileService` sends after successful creation/obsolete-state persistence.
It requires the configuration below. Consumer hosts opt in separately.

Shared serialization and settlement logic lives in `FileBusSenderBase` and
`FileBusReceiverBase`. Each concrete class initializes, starts/stops, and disposes
only its own queue resource. The Service Bus client is shared; creating a sender or
receiver never creates the other operation's sender or processor.

`FileService` depends on `ICreateWordFileSender` and `IDeleteWordFileSender`. Message IDs are
`create-word-file:{fileId}` and `delete-word-file:{fileId}` so retries for the same
persisted file operation reuse the ID. Send failures propagate to the API; database
changes are not rolled back because a timed-out send may already have succeeded.
There is still a save/send failure window until a transactional outbox is added.

## Configuration and registration

```json
{
  "ArchiveroFileBus": {
    "ConnectionString": "<namespace connection string from a secret store>",
    "CreateWordFileQueueName": "create-word-file",
    "DeleteWordFileQueueName": "delete-word-file"
  }
}
```

Use a namespace connection string (not one restricted to a single EntityPath).
Keep credentials out of source control, for example in the environment variable
`ArchiveroFileBus__ConnectionString`. Provision both queues before starting the host.

```csharp
using Demo.Archivero.Infrastructure.ServiceBus;

builder.Services.AddArchiveroFileBus(builder.Configuration);
// Consuming hosts additionally register their scoped, idempotent handler:
builder.Services.AddArchiveroFileBusReceiver<MyWordFileMessageHandler>();
```

Inject `ICreateWordFileSender` and/or `IDeleteWordFileSender` into producers:

```csharp
await createSender.SendCreateWordFileAsync(fileId, username, persistedOperationId, ct);
await deleteSender.SendDeleteWordFileAsync(fileId, username, persistedDeleteOperationId, ct);
```

Use a different ID for each business operation, and persist/reuse that ID when
retrying the same operation. Do not generate a fresh ID for each send attempt.
The SDK retries transient send errors; final errors propagate to the caller.

Implement `IWordFileMessageHandler`. Each method receives the payload, message ID,
and cancellation token. Return `true` only after durable success or after detecting
that this message was already processed. Return `false` for an unsuccessful result,
or throw. A `ResultDto<bool>` with a failed result must never be mapped to `true`.
The converter worker registers a scoped handler that delegates to `WordFileService`.
Its blob/database operations still need full idempotency and recovery across partial
failures to safely handle all redelivery scenarios.

## Delivery and recovery

Both processors use PeekLock, disable automatic completion and prefetch, process
one message at a time per queue, and renew locks automatically for up to 30 minutes.
Only a successful handler is followed by explicit completion. A false result is
abandoned; a thrown exception is left to the SDK's automatic abandonment. Shutdown,
process crashes, and failed settlement leave uncompleted messages available for
redelivery when their locks expire. Processing longer than the renewal window can
also cause redelivery; handlers must honor cancellation and tolerate duplicates.

Invalid JSON, invalid fields, or an incorrect operation subject are explicitly
dead-lettered, preserving the original message for inspection and replay. Retry
exhaustion (`MaxDeliveryCount`) also moves messages to the queue's dead-letter queue.
Create and delete queues are independent: no ordering between them is guaranteed.

PeekLock provides **at-least-once delivery**, not an absolute no-loss or exactly-once
guarantee. To meet an end-to-end no-loss requirement in deployment:

- Enable dead-lettering on message expiration for both queues, set appropriate
  retention/TTL and maximum delivery counts, and disable automatic queue deletion.
  The sender requests maximum TTL, but the entity's TTL still caps retention.
- Monitor and alert on both dead-letter queues; retain and replay failed work after
  correcting its cause. This library does not silently drain or purge dead letters.
- Enable duplicate detection on both queues and choose a suitable detection window.
  Stable message IDs support send retries; receivers still need durable idempotency.
- Persist pending sends in a transactional outbox together with database changes.
  Keep retrying until broker acknowledgement. A send timeout can mean the broker
  accepted the message; retry with the same ID. This transport alone cannot recover
  work lost by a producer before sending or between a database commit and a send.
- Make blob and database side effects recoverable/idempotent, including when work
  succeeds but completion fails. Complete only after all required work is durable.

The client, senders, and processors are reused and asynchronously disposed by DI.
The consuming host starts/stops both processors and creates an async DI scope per
delivery. Broker errors are logged without logging message bodies or credentials.

References: [Azure message loss and duplicates](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-message-loss-and-duplicates)
and [message settlement](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement).
