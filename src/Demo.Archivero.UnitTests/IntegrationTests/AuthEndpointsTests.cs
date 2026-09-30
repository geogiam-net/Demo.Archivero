using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Demo.Archivero.Application.Dtos.Auth;
using Demo.Archivero.Application.Security;
using Demo.Archivero.Domain.Enums;
using NUnit.Framework;

namespace Demo.Archivero.UnitTests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class AuthEndpointsTests
{
    [Test]
    public async Task Login_returns_token_and_user_details_and_token_authenticates_me()
    {
        await using var host = await EndpointTestHost.StartAsync();
        var expected = new LoginResponseDto(host.CreateToken(), "alice", AppRoles.Admin,
            [AppPermissions.ArchiveroView], new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        host.Auth.Result = new(expected);

        using var response = await host.Client.PostAsJsonAsync("/api/auth/login", new { username = "alice", password = "test-password" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.Multiple(() =>
        {
            Assert.That(host.Auth.Request, Is.EqualTo(("alice", "test-password")));
            Assert.That(login!.Token, Is.EqualTo(expected.Token));
            Assert.That(login.Username, Is.EqualTo(expected.Username));
            Assert.That(login.Role, Is.EqualTo(expected.Role));
            Assert.That(login.Permissions, Is.EqualTo(expected.Permissions));
            Assert.That(login.ExpiresAtUtc, Is.EqualTo(expected.ExpiresAtUtc));
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        using var meResponse = await host.Client.GetAsync("/api/auth/me");
        Assert.That(meResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.That(me!.Username, Is.EqualTo("alice"));
    }

    [TestCase(Error.ValidationError, HttpStatusCode.BadRequest)]
    [TestCase(Error.NotAuthorized, HttpStatusCode.Unauthorized)]
    [TestCase(Error.InternalServerError, HttpStatusCode.InternalServerError)]
    public async Task Login_maps_service_errors(Error error, HttpStatusCode status)
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.Auth.Result = new(null, error, ["Login failed"]);
        using var response = await host.Client.PostAsJsonAsync("/api/auth/login", new { username = "alice", password = "incorrect" });
        Assert.That(response.StatusCode, Is.EqualTo(status));
        if (error != Error.NotAuthorized)
            Assert.That(await response.Content.ReadFromJsonAsync<string[]>(), Is.EqualTo(new[] { "Login failed" }));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Not.Contain("incorrect"));
    }

    [Test]
    public async Task Login_rejects_malformed_json_before_calling_service()
    {
        await using var host = await EndpointTestHost.StartAsync();
        using var body = new StringContent("{broken", Encoding.UTF8, "application/json");
        using var response = await host.Client.PostAsync("/api/auth/login", body);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(host.Auth.Request, Is.Null);
    }

    [TestCase(null, AppRoles.Viewer)]
    [TestCase(AppRoles.Admin, AppRoles.Admin)]
    [TestCase(AppRoles.Viewer, AppRoles.Viewer)]
    public async Task Me_returns_identity_role_and_permissions(string? role, string expectedRole)
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.CreateToken("bob", role));
        using var response = await host.Client.GetAsync("/api/auth/me");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var me = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.Multiple(() =>
        {
            Assert.That(me!.Username, Is.EqualTo("bob"));
            Assert.That(me.Role, Is.EqualTo(expectedRole));
            Assert.That(me.Permissions, Is.EqualTo(new[] { AppPermissions.ArchiveroView }));
        });
    }

    [TestCase("missing")]
    [TestCase("malformed")]
    [TestCase("expired")]
    [TestCase("issuer")]
    [TestCase("audience")]
    public async Task Me_rejects_missing_or_invalid_bearer_tokens(string scenario)
    {
        await using var host = await EndpointTestHost.StartAsync();
        var token = scenario switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "expired" => host.CreateToken(expired: true),
            "issuer" => host.CreateToken(issuer: "untrusted"),
            _ => host.CreateToken(audience: "another-api")
        };
        if (token is not null) host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await host.Client.GetAsync("/api/auth/me");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.WwwAuthenticate.Any(x => x.Scheme == "Bearer"), Is.True);
    }
}
