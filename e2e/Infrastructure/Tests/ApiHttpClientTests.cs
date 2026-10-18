using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using e2e.Infrastructure.Http;
using NUnit.Framework;

namespace e2e.Infrastructure.Tests;

[TestFixture]
[Category("Infrastructure")]
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ApiHttpClientTests
{
    [TestCase("https://example.test/services", "public/organizations?limit=2", "https://example.test/services/public/organizations?limit=2")]
    [TestCase("https://example.test/services/", "/api/config?test=1", "https://example.test/api/config?test=1")]
    public async Task ComposesUrlsAndSerializesJson(string baseUrl, string path, string expectedUrl)
    {
        //Arrange
        string? url = null, body = null, contentType = null;
        using var handler = new StubHttpMessageHandler(async (request, _) =>
        {
            url = request.RequestUri!.ToString();
            body = await request.Content!.ReadAsStringAsync();
            contentType = request.Content.Headers.ContentType!.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"userId\":\"user-1\"}") };
        });
        using var client = new ApiHttpClient(baseUrl, handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { UserId = "user-1" }) };

        //Act
        var result = await client.SendJsonAsync<ApiResponse>(request, "Test request");

        //Assert
        Assert.Multiple(() =>
        {
            Assert.That(url, Is.EqualTo(expectedUrl));
            Assert.That(body, Is.EqualTo("{\"userId\":\"user-1\"}"));
            Assert.That(contentType, Is.EqualTo("application/json"));
            Assert.That(result.UserId, Is.EqualTo("user-1"));
        });
    }

    [TestCase("", "empty")]
    [TestCase("null", "empty")]
    [TestCase("{\"private\":\"test-secret\", broken}", "invalid")]
    public void RejectsInvalidResponsesWithoutExposingContent(string body, string failure)
    {
        //Arrange
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        using var client = new ApiHttpClient("https://example.test", handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "test");

        //Act
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => client.SendJsonAsync<JsonElement?>(request, "Test request"));

        //Assert
        Assert.That(error!.Message, Is.EqualTo($"Test request returned an {failure} response."));
        Assert.That(error.ToString(), Does.Not.Contain("test-secret"));
        Assert.That(error.InnerException, Is.Null);
    }

    [Test]
    public async Task AcceptsExplicitlyOptionalEmptyResponsesAndDisposesContent()
    {
        //Arrange
        var content = new StringContent("");
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NoContent) { Content = content }));
        using var client = new ApiHttpClient("https://example.test", handler);
        using var request = new HttpRequestMessage(HttpMethod.Put, "test");

        //Act
        var result = await client.SendJsonAsync<JsonElement>(request, "Test request", allowEmptyResponse: true);

        //Assert
        Assert.That(result.ValueKind, Is.EqualTo(JsonValueKind.Undefined));
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await content.ReadAsStringAsync());
    }

    [Test]
    public void HttpFailuresAreSafeAndNeverRetried()
    {
        //Arrange
        var content = new StringContent("test-secret");
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = content }));
        using var client = new ApiHttpClient("https://example.test", handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "test?token=test-secret");

        //Act
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request, "Test request"));

        //Assert
        Assert.That(error!.Message, Is.EqualTo("Test request failed with HTTP 400."));
        Assert.That(handler.Calls, Is.EqualTo(1));
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await content.ReadAsStringAsync());
    }

    [Test]
    public async Task RawForwardingPreservesHeadersQueryStatusAndBody()
    {
        //Arrange
        string? url = null, authorization = null;
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            url = request.RequestUri!.ToString();
            authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("upstream body") });
        });
        using var client = new ApiHttpClient("https://example.test", handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/sync?excludeDomains=true");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer forwarded-token");

        //Act
        using var response = await client.SendRawAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        //Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(body, Is.EqualTo("upstream body"));
        Assert.That(url, Is.EqualTo("https://example.test/api/sync?excludeDomains=true"));
        Assert.That(authorization, Is.EqualTo("Bearer forwarded-token"));
    }

    [Test]
    public async Task CancellationReachesHandlerAndDisposalReleasesIt()
    {
        //Arrange
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHttpMessageHandler(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new ApiHttpClient("https://example.test", handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "test");
        using var cancellation = new CancellationTokenSource();

        //Act
        var pending = client.SendAsync(request, "Test request", cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var error = Assert.CatchAsync<OperationCanceledException>(async () => await pending);
        client.Dispose();

        //Assert
        Assert.That(error, Is.Not.Null);
        Assert.That(handler.Calls, Is.EqualTo(1));
        Assert.That(handler.Disposed, Is.True);
    }

    private sealed record ApiResponse(string UserId);
}
