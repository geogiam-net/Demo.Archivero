using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Demo.Archivero.Api.Endpoints;
using Demo.Archivero.Api.Startup;
using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Application.Dtos.Auth;
using Demo.Archivero.Application.Dtos.File;
using Demo.Archivero.Application.Interfaces.Application;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace Demo.Archivero.UnitTests.IntegrationTests;

internal sealed class EndpointTestHost : IAsyncDisposable
{
    private const string SigningKey = "endpoint-tests-only-not-a-production-secret-64-characters-long-key";
    private WebApplication app = null!;
    public HttpClient Client { get; private set; } = null!;
    public AuthStub Auth { get; } = new();
    public FileStub Files { get; } = new();
    public Mock<IRedisDatabase> Redis { get; } = new(MockBehavior.Strict);
    public IReadOnlyList<FileDto>? CachedFiles { get; set; }
    public TimeSpan? CacheLifetime { get; private set; }
    public int CacheRemovals { get; private set; }

    public static async Task<EndpointTestHost> StartAsync()
    {
        var host = new EndpointTestHost();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArchiveroJwt:Issuer"] = "integration-tests",
            ["ArchiveroJwt:Audience"] = "integration-tests-client",
            ["ArchiveroJwt:SigningKey"] = SigningKey,
            ["ArchiveroJwt:ExpirationMinutes"] = "10"
        });
        builder.Services.AddAuthorization(builder.Configuration);
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton<IAuthService>(host.Auth);
        builder.Services.AddSingleton<IFileService>(host.Files);
        builder.Services.AddSingleton(host.Redis.Object);
        host.Redis.Setup(x => x.GetAsync<IReadOnlyList<FileDto>>("/api/files", It.IsAny<CommandFlags>()))
            .Returns(() => Task.FromResult(host.CachedFiles));
        host.Redis.Setup(x => x.AddAsync("/api/files", It.IsAny<IReadOnlyList<FileDto>>(), It.IsAny<TimeSpan>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>(), It.IsAny<HashSet<string>>()))
            .Returns((string key, IReadOnlyList<FileDto> value, TimeSpan lifetime, When when, CommandFlags flags, HashSet<string> tags) =>
            {
                host.CachedFiles = value;
                host.CacheLifetime = lifetime;
                return Task.FromResult(true);
            });
        host.Redis.Setup(x => x.RemoveAsync("/api/files", It.IsAny<CommandFlags>())).Returns(() =>
        {
            host.CachedFiles = null;
            host.CacheRemovals++;
            return Task.FromResult(true);
        });
        host.app = builder.Build();
        host.app.UseAuthentication();
        host.app.UseAuthorization();
        host.app.UseStatusCodePages();
        host.app.UseExceptionHandler();
        host.app.MapAuthEndpoints();
        host.app.MapFileEndpoints();
        await host.app.StartAsync();
        host.Client = host.app.GetTestClient();
        return host;
    }

    public string CreateToken(string username = "alice", string? role = AppRoles.Admin,
        string issuer = "integration-tests", string audience = "integration-tests-client", bool expired = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, username) };
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, audience, claims, now.AddHours(-1),
            expired ? now.AddMinutes(-10) : now.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void SignIn(string username = "alice") =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(username));

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }

    internal sealed class AuthStub : IAuthService
    {
        public ResultDto<LoginResponseDto?> Result { get; set; } = new(null);
        public (string Username, string Password)? Request { get; private set; }
        public Task<ResultDto<LoginResponseDto?>> LoginAsync(string username, string password, CancellationToken ct)
        {
            Request = (username, password);
            return Task.FromResult(Result);
        }
    }

    internal sealed class FileStub : IFileService
    {
        public ResultDto<bool> MutationResult { get; set; } = new(true);
        public ResultDto<IReadOnlyList<FileDto>> ListResult { get; set; } = new(Array.Empty<FileDto>());
        public (string Title, string Content, string Username)? Creation { get; private set; }
        public (int FileId, string Username)? Deletion { get; private set; }
        public List<string> ListRequests { get; } = [];

        public Task<ResultDto<bool>> QueueFileAsync(string title, string content, string username, CancellationToken ct)
        {
            Creation = (title, content, username);
            return Task.FromResult(MutationResult);
        }

        public Task<ResultDto<IReadOnlyList<FileDto>>> GetFilesAsync(string username, CancellationToken ct)
        {
            ListRequests.Add(username);
            return Task.FromResult(ListResult);
        }

        public Task<ResultDto<bool>> DeleteFileAsync(int fileId, string username, CancellationToken ct)
        {
            Deletion = (fileId, username);
            return Task.FromResult(MutationResult);
        }
    }
}
