using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using e2e.SeederApi;
using e2e.ServerApi;
using NUnit.Framework;

namespace e2e.Infrastructure.Tests;

[TestFixture]
[Category("Infrastructure")]
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ApiClientTests
{
    [Test]
    public async Task SeederPreservesAuthenticationPlayHeadersAndEscapedCleanup()
    {
        //Arrange
        var calls = new List<(string Method, string Url, string? Auth, string? Play, string? Body)>();
        using var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            calls.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Play-Id", out var values) ? values.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync()));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"result\":{\"id\":1},\"mangleMap\":{}}") };
        });
        using var client = new SeederApiClient(new SeederApiSettings("https://seeder.test/base", "test-user", "test-password"), handler);

        //Act
        await client.SeedAsync<JsonElement>("play-a", "Scene", new { name = "test" });
        await client.QueryAsync<JsonElement>("play-b", "Query", new { id = 1 });
        await client.DeleteAsync("play/a b");

        //Assert
        Assert.That(calls.Select(c => c.Auth), Is.All.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-user:test-password"))));
        Assert.That(calls.Select(c => c.Method), Is.EqualTo(new[] { "POST", "POST", "DELETE" }));
        Assert.That(calls.Select(c => c.Play), Is.EqualTo(new string?[] { "play-a", "play-b", null }));
        Assert.That(calls.Select(c => c.Url), Is.EqualTo(new[] { "https://seeder.test/base/seed", "https://seeder.test/base/query", "https://seeder.test/base/seed/play%2Fa%20b" }));
        Assert.That(calls[0].Body, Is.EqualTo("{\"template\":\"Scene\",\"arguments\":{\"name\":\"test\"}}"));
    }

    [Test]
    public async Task ServerAuthenticatesWithFormThenIsolatesBearerAndPlayHeaders()
    {
        //Arrange
        var calls = new List<(string Method, string Path, string? Auth, string? Play, string? Version, string? Type, string Body)>();
        using var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Play-Id", out var plays) ? plays.Single() : null,
                request.Headers.TryGetValues("Bitwarden-Client-Version", out var versions) ? versions.Single() : null,
                request.Content!.Headers.ContentType!.MediaType, await request.Content.ReadAsStringAsync()));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"test-token\"}") };
        });
        using var client = new ServerApiClient("https://server.test", handler);
        using var other = new ServerApiClient("https://server.test", handler: new StubHttpMessageHandler((request, _) =>
        {
            calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(), null, null, null, ""));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }));
        var account = new SeededAccount(" Test@Example.com ", "test-password", "test-id");
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(account.Password), Encoding.UTF8.GetBytes("test@example.com"), 2, HashAlgorithmName.SHA256, 32);
        var expectedHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(key, Encoding.UTF8.GetBytes(account.Password), 1, HashAlgorithmName.SHA256, 32));

        //Act
        await client.AuthenticateAsync(account, 2, "play-a");
        await client.SendAsync(HttpMethod.Put, "two-factor/email", new { token = "123456" });
        await client.AuthenticateAsync(account, 2, "play-b");
        await client.SendAsync(HttpMethod.Post, "two-factor/get-email", new { masterPasswordHash = client.MasterPasswordHash });
        await other.SendAsync(HttpMethod.Get, "config", new { });
        var fields = calls[0].Body.Split('&').Select(part => part.Split('=', 2))
            .ToDictionary(parts => WebUtility.UrlDecode(parts[0]), parts => WebUtility.UrlDecode(parts[1]));

        //Assert
        Assert.That(calls.Select(c => c.Auth), Is.EqualTo(new string?[] { null, "Bearer test-token", null, "Bearer test-token", null }));
        Assert.That(calls.Select(c => c.Play), Is.EqualTo(new string?[] { null, "play-a", null, "play-b", null }));
        Assert.That(calls[0].Path, Is.EqualTo("/identity/connect/token"));
        Assert.That(calls[0].Type, Is.EqualTo("application/x-www-form-urlencoded"));
        Assert.That(calls[0].Version, Is.EqualTo("2026.10.0"));
        Assert.That(fields["password"], Is.EqualTo(expectedHash));
        Assert.That(fields["grant_type"], Is.EqualTo("password"));
        Assert.That(fields["scope"], Is.EqualTo("api offline_access"));
        Assert.That(calls[1].Path, Is.EqualTo("/api/two-factor/email"));
        Assert.That(calls[1].Type, Is.EqualTo("application/json"));
        Assert.That(calls[1].Body, Is.EqualTo("{\"token\":\"123456\"}"));
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"access_token\":null}")]
    [TestCase("{\"access_token\":123}")]
    [TestCase("{\"access_token\":\"\"}")]
    public void ServerRejectsMissingOrInvalidAccessToken(string body)
    {
        //Arrange
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        using var client = new ServerApiClient("https://server.test", handler);

        //Act
        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.AuthenticateAsync(new SeededAccount("test@example.com", "test-password", "test-id"), 2, "play-a"));

        //Assert
        Assert.That(error!.Message, Is.EqualTo("Server API authentication returned no access token."));
    }
}
