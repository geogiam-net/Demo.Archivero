namespace Demo.Archivero.Application.Interfaces;

/// <summary>
/// Abstraction over the system clock. Application code must never read the current time
/// through the static <see cref="DateTime"/> / <see cref="DateTimeOffset"/> members;
/// it depends on this interface instead, so time can be controlled in tests and the
/// domain stays free of ambient dependencies. The implementation lives in the web layer.
///
/// Mapping to the static members it replaces:
/// <list type="bullet">
///   <item><see cref="UtcNow"/> == DateTime.UtcNow</item>
///   <item><see cref="Now"/> == DateTime.Now</item>
///   <item><see cref="UtcNowOffset"/> == DateTimeOffset.UtcNow</item>
///   <item><see cref="NowOffset"/> == DateTimeOffset.Now</item>
/// </list>
/// (DateTime.Today is <c>Now.Date</c>.)
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Current UTC instant as a <see cref="DateTime"/> (== DateTime.UtcNow).</summary>
    DateTime UtcNow { get; }

    /// <summary>Current local instant as a <see cref="DateTime"/> (== DateTime.Now).</summary>
    DateTime Now { get; }

    /// <summary>Current UTC instant as a <see cref="DateTimeOffset"/> (== DateTimeOffset.UtcNow).</summary>
    DateTimeOffset UtcNowOffset { get; }

    /// <summary>Current local instant as a <see cref="DateTimeOffset"/> (== DateTimeOffset.Now).</summary>
    DateTimeOffset NowOffset { get; }
}
