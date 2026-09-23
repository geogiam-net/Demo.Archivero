using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Dtos.User;
using Demo.Archivero.Application.Interfaces;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Interfaces.Repositories;
using Demo.Archivero.Application.Services;
using Demo.Archivero.Domain.Entities;
using Demo.Archivero.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using FileEntity = Demo.Archivero.Domain.Entities.File;

namespace Demo.Archivero.UnitTests;

public class FileServiceTests
{
    [Test]
    public async Task Creation_sends_persisted_id_and_canonical_username_after_save()
    {
        var repository = new Repository();
        var sender = new Sender(repository);
        using var cancellation = new CancellationTokenSource();
        var result = await CreateService(repository, sender).QueueFileAsync(" Title ", "Content", "ALICE", cancellation.Token);
        Assert.That(result.Result, Is.True);
        Assert.That(repository.SavedFile!.Title, Is.EqualTo("Title"));
        Assert.That(sender.Messages.Single(), Is.EqualTo(("create", 42, "alice", "create-word-file:42", cancellation.Token)));
    }

    [TestCase(Error.InternalServerError, 0)]
    [TestCase(Error.Conflict, 42)]
    [TestCase(Error.None, 0)]
    public async Task Failed_creation_does_not_send(Error error, int id)
    {
        var repository = new Repository { CreateResult = new(id, error, new[] { "Save failed" }) };
        var sender = new Sender(repository);
        var result = await CreateService(repository, sender).QueueFileAsync("Title", "Content", "alice", default);
        Assert.That(result.Result, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo(error == Error.None ? Error.InternalServerError : error));
        Assert.That(sender.Messages, Is.Empty);
    }

    [Test]
    public async Task Deletion_sends_only_after_marking_obsolete_and_reuses_operation_id()
    {
        var repository = new Repository();
        var sender = new Sender(repository);
        var service = CreateService(repository, sender);
        using var cancellation = new CancellationTokenSource();
        await service.DeleteFileAsync(42, "ALICE", cancellation.Token);
        var result = await service.DeleteFileAsync(42, "ALICE", cancellation.Token);
        Assert.That(result.Result, Is.True);
        Assert.That(sender.Messages, Has.Count.EqualTo(2));
        Assert.That(sender.Messages[0], Is.EqualTo(("delete", 42, "alice", "delete-word-file:42", cancellation.Token)));
        Assert.That(sender.Messages[1], Is.EqualTo(sender.Messages[0]));
    }

    [TestCase(false, Error.NotFound)]
    [TestCase(false, Error.None)]
    [TestCase(true, Error.InternalServerError)]
    public async Task Failed_obsolete_update_does_not_send(bool success, Error error)
    {
        var repository = new Repository { DeleteResult = new(success, error) };
        var sender = new Sender(repository);
        var result = await CreateService(repository, sender).DeleteFileAsync(42, "alice", default);
        Assert.That(result, Is.SameAs(repository.DeleteResult));
        Assert.That(sender.Messages, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Send_failure_is_not_reported_as_success(bool delete)
    {
        var repository = new Repository();
        var sender = new Sender(repository) { Error = new InvalidOperationException("Broker unavailable") };
        var service = CreateService(repository, sender);
        Assert.ThrowsAsync<InvalidOperationException>(() => delete
            ? service.DeleteFileAsync(42, "alice", default)
            : service.QueueFileAsync("Title", "Content", "alice", default));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Missing_user_never_saves_or_sends(bool delete)
    {
        var repository = new Repository();
        var sender = new Sender(repository);
        var service = CreateService(repository, sender, new Users { User = null });
        var result = delete ? await service.DeleteFileAsync(42, "missing", default)
            : await service.QueueFileAsync("Title", "Content", "missing", default);
        Assert.That(result.ErrorCode, Is.EqualTo(Error.NotFound));
        Assert.That(repository.Saved, Is.False);
        Assert.That(sender.Messages, Is.Empty);
    }

    private static FileService CreateService(Repository repository, Sender sender, Users? users = null)
        => new(repository, users ?? new Users(), NullLogger<FileService>.Instance, new Clock(), null!, sender, sender);

    private sealed class Sender(Repository repository) : ICreateWordFileSender, IDeleteWordFileSender
    {
        public List<(string Operation, int Id, string Username, string MessageId, CancellationToken Token)> Messages { get; } = new();
        public Exception? Error { get; init; }
        public Task SendCreateWordFileAsync(int fileId, string username, string messageId, CancellationToken ct)
            => Send("create", fileId, username, messageId, ct);
        public Task SendDeleteWordFileAsync(int fileId, string username, string messageId, CancellationToken ct)
            => Send("delete", fileId, username, messageId, ct);
        private Task Send(string operation, int id, string username, string messageId, CancellationToken ct)
        {
            Assert.That(repository.Saved, Is.True, "The repository must finish before publishing.");
            if (Error is not null) return Task.FromException(Error);
            Messages.Add((operation, id, username, messageId, ct));
            return Task.CompletedTask;
        }
    }

    private sealed class Repository : IFileRepository
    {
        public ResultDto<int> CreateResult { get; init; } = new(42);
        public ResultDto<bool> DeleteResult { get; init; } = new(true);
        public bool Saved { get; private set; }
        public FileEntity? SavedFile { get; private set; }
        public Task<ResultDto<int>> SetInQueueFileAsync(FileEntity file, AppUser user, CancellationToken ct)
        {
            Saved = true;
            SavedFile = file;
            return Task.FromResult(CreateResult);
        }
        public Task<ResultDto<bool>> SetFileAsObsoleteAsync(int id, AppUser user, CancellationToken ct)
        {
            Saved = true;
            return Task.FromResult(DeleteResult);
        }
        public Task<FileEntity?> GetFileAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<FileEntity>> GetFilesAsync(int owner, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultDto<bool>> ReadyFileAsync(FileEntity file, AppUser user, string blobId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultDto<bool>> DeleteFileAsync(int id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Users : IUserRepository
    {
        public AppUser? User { get; init; } = new() { Id = 1, Username = "alice", PasswordHash = "unused" };
        public Task<AppUser?> GetUserAsync(string username, CancellationToken ct) => Task.FromResult(User);
        public Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task CreateUserAsync(CreateUserDto dto, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateUserAsync(int id, UpdateUserDto dto, CancellationToken ct) => throw new NotSupportedException();
        public Task ResetPasswordAsync(int id, string password, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Clock : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        public DateTime Now => UtcNow;
        public DateTimeOffset UtcNowOffset => new(UtcNow);
        public DateTimeOffset NowOffset => UtcNowOffset;
    }
}

