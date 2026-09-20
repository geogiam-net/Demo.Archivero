using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demo.Archivero.Converter.Client.Contracts;

namespace Demo.Archivero.Converter.Client;

public sealed class ConverterApiClient(HttpClient httpClient) : IConverterApiClient
{
    public async Task<ConverterPingResponse> PingAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/ping", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ConverterPingResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Converter API returned an empty ping response.");
    }

    public Task CreateWordFileAsync(
        CreateWordFileRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SendAuthenticatedAsync(
            HttpMethod.Post,
            "api/wordfiles",
            JsonContent.Create(request),
            ct);
    }

    public Task DeleteWordFileAsync(
        int fileId,
        string username,
        CancellationToken ct)
    {
        if (fileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileId));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        var path = $"api/wordfiles/{fileId}/user/{Uri.EscapeDataString(username)}";
        return SendAuthenticatedAsync(HttpMethod.Delete, path, null, ct);
    }

    private async Task SendAuthenticatedAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };

        using var response = await httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Converter API returned {(int)response.StatusCode} ({response.StatusCode}): {detail}",
            null,
            response.StatusCode);
    }
}
