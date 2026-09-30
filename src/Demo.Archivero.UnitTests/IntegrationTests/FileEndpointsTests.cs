using System.Net;
using System.Net.Http.Json;
using System.Text;
using Demo.Archivero.Application.Dtos.File;
using Demo.Archivero.Domain.Enums;
using NUnit.Framework;

namespace Demo.Archivero.UnitTests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class FileEndpointsTests
{
    private static FileDto Document => new(42, "Pourquoi l'utiliser — été", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "https://example.test/document.docx");

    [TestCase("GET", "/api/files")]
    [TestCase("POST", "/api/files")]
    [TestCase("DELETE", "/api/files/42")]
    public async Task File_routes_require_authentication_without_invoking_dependencies(string method, string path)
    {
        await using var host = await EndpointTestHost.StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { title = "Title", content = "Text" });
        using var response = await host.Client.SendAsync(request);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(host.Files.Creation, Is.Null);
            Assert.That(host.Files.Deletion, Is.Null);
            Assert.That(host.Files.ListRequests, Is.Empty);
            Assert.That(host.Redis.Invocations, Is.Empty);
        });
    }

    [Test]
    public async Task Create_binds_json_uses_authenticated_identity_and_invalidates_cache()
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.SignIn("alice");
        host.CachedFiles = [];
        using var response = await host.Client.PostAsJsonAsync("/api/files", new
        {
            title = Document.Title, content = "Hello <world>", username = "someone-else"
        });
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(host.Files.Creation, Is.EqualTo((Document.Title, "Hello <world>", "alice")));
            Assert.That(host.CacheRemovals, Is.EqualTo(1));
            Assert.That(host.CachedFiles, Is.Null);
        });
    }

    [Test]
    public async Task Delete_binds_id_uses_authenticated_identity_and_invalidates_cache()
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.SignIn("bob");
        host.CachedFiles = [Document];
        using var response = await host.Client.DeleteAsync("/api/files/42");
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(host.Files.Deletion, Is.EqualTo((42, "bob")));
            Assert.That(host.CacheRemovals, Is.EqualTo(1));
            Assert.That(host.CachedFiles, Is.Null);
        });
    }

    [TestCase(Error.ValidationError, HttpStatusCode.BadRequest)]
    [TestCase(Error.NotFound, HttpStatusCode.NotFound)]
    [TestCase(Error.Conflict, HttpStatusCode.Conflict)]
    [TestCase(Error.NotAuthorized, HttpStatusCode.Unauthorized)]
    [TestCase(Error.InternalServerError, HttpStatusCode.InternalServerError)]
    public async Task Failed_mutations_map_errors_and_preserve_cache(Error error, HttpStatusCode status)
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.SignIn();
        host.CachedFiles = [Document];
        host.Files.MutationResult = new(false, error, ["Operation failed"]);
        using var create = await host.Client.PostAsJsonAsync("/api/files", new { title = "Title", content = "Text" });
        using var delete = await host.Client.DeleteAsync("/api/files/42");
        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(status));
            Assert.That(delete.StatusCode, Is.EqualTo(status));
            Assert.That(host.CacheRemovals, Is.Zero);
            Assert.That(host.CachedFiles, Is.EqualTo(new[] { Document }));
        });
        if (error != Error.NotAuthorized)
        {
            Assert.That(await create.Content.ReadFromJsonAsync<string[]>(), Is.EqualTo(new[] { "Operation failed" }));
            Assert.That(await delete.Content.ReadFromJsonAsync<string[]>(), Is.EqualTo(new[] { "Operation failed" }));
        }
    }

    [Test]
    public async Task List_returns_document_contract_and_reuses_cached_result()
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.SignIn();
        host.Files.ListResult = new(new[] { Document });
        for (var i = 0; i < 2; i++)
        {
            using var response = await host.Client.GetAsync("/api/files");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await response.Content.ReadFromJsonAsync<FileDto[]>(), Is.EqualTo(new[] { Document }));
        }
        Assert.That(host.Files.ListRequests, Is.EqualTo(new[] { "alice" }));
        Assert.That(host.CacheLifetime, Is.EqualTo(TimeSpan.FromMinutes(2)));
    }

    [Test]
    public async Task List_returns_not_found_and_caches_empty_result()
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.SignIn();
        for (var i = 0; i < 2; i++)
        {
            using var response = await host.Client.GetAsync("/api/files");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
        Assert.That(host.Files.ListRequests, Has.Count.EqualTo(1));
        Assert.That(host.CachedFiles, Is.Empty);
        Assert.That(host.CacheLifetime, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public async Task Successful_creation_invalidates_negative_cache_so_next_list_reads_service()
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.SignIn();
        using var empty = await host.Client.GetAsync("/api/files");
        Assert.That(empty.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var create = await host.Client.PostAsJsonAsync("/api/files", new { title = Document.Title, content = "Text" });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        host.Files.ListResult = new(new[] { Document });
        using var response = await host.Client.GetAsync("/api/files");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadFromJsonAsync<FileDto[]>(), Is.EqualTo(new[] { Document }));
        Assert.That(host.Files.ListRequests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Invalid_requests_do_not_invoke_services_or_change_cache()
    {
        await using var host = await EndpointTestHost.StartAsync();
        host.SignIn();
        using var body = new StringContent("{broken", Encoding.UTF8, "application/json");
        using var create = await host.Client.PostAsync("/api/files", body);
        using var delete = await host.Client.DeleteAsync("/api/files/not-an-integer");
        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(host.Files.Creation, Is.Null);
            Assert.That(host.Files.Deletion, Is.Null);
            Assert.That(host.Redis.Invocations, Is.Empty);
        });
    }
}
