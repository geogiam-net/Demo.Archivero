using Demo.Archivero.Application.Interfaces;

namespace Demo.Archivero.Converter.Api.Services;

/// <summary>
/// Default <see cref="IDateTimeProvider"/> backed by the real system clock.
/// Registered as a singleton; swap it in tests for a controllable fake.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    [Obsolete("Avoid if possible")]
    public DateTime Now => DateTime.Now;

    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;

    [Obsolete("Avoid if possible")]
    public DateTimeOffset NowOffset => DateTimeOffset.Now;
}