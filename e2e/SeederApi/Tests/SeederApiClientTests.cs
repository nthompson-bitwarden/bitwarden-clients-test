using System.Net;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace e2e.SeederApi.Tests;

[TestFixture]
[Category("Infrastructure")]
public sealed class SeederApiClientTests
{
    [Test]
    public async Task ProvisioningUsesMangledIdentityAndEncryptedVaultScenes()
    {
        //Arrange
        var handler = new RecordingHandler();
        await using var session = new SeederSession(CreateClient(handler));

        //Act
        await session.ProvisionAsync();
        await session.DisposeAsync();

        //Assert
        Assert.That(session.Account.Email, Is.EqualTo("mangled@example.com"));
        Assert.That(handler.Calls.Select(c => c.Template), Is.EqualTo(new[]
            { "SingleUserScene", "UserFolderScene", "UserLoginCipherScene", "" }));
        Assert.That(handler.Calls.Take(3).Select(c => c.PlayId), Is.All.EqualTo(session.PlayId));
        Assert.That(handler.Calls.All(c => c.Authorization == "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test-user:test-password"))), Is.True);
        var folder = handler.Calls[1].Body.GetProperty("arguments");
        Assert.That(folder.GetProperty("userKeyB64").GetString(), Is.EqualTo("test-key"));
        Assert.That(folder.GetProperty("userId").GetString(), Is.EqualTo(session.Account.UserId));
        Assert.That(handler.Calls[^1].Path, Is.EqualTo("/seed/" + session.PlayId));
        Assert.That(handler.Calls.Count(c => c.Method == HttpMethod.Delete), Is.EqualTo(1));
    }

    [Test]
    public async Task SessionsUseDistinctPlayIdsAndCleanOnlyTheirOwnData()
    {
        //Arrange
        var firstHandler = new RecordingHandler();
        var secondHandler = new RecordingHandler();
        await using var first = new SeederSession(CreateClient(firstHandler));
        await using var second = new SeederSession(CreateClient(secondHandler));

        //Act
        await Task.WhenAll(first.ProvisionAsync(), second.ProvisionAsync());
        await first.DisposeAsync();
        await second.DisposeAsync();

        //Assert
        Assert.That(first.PlayId, Is.Not.EqualTo(second.PlayId));
        Assert.That(firstHandler.Calls[^1].Path, Is.EqualTo("/seed/" + first.PlayId));
        Assert.That(secondHandler.Calls[^1].Path, Is.EqualTo("/seed/" + second.PlayId));
    }

    [TestCase("SingleUserScene", 1)]
    [TestCase("UserFolderScene", 2)]
    [TestCase("UserLoginCipherScene", 3)]
    public void PartialProvisioningFailureDeletesPlayWithoutRetryingMutation(string failedTemplate, int seedCalls)
    {
        //Arrange
        var handler = new RecordingHandler { FailTemplate = failedTemplate };
        var session = new SeederSession(CreateClient(handler));

        //Act
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => session.ProvisionAsync());

        //Assert
        Assert.That(error!.Message, Does.Contain(failedTemplate).And.Contain("HTTP 400"));
        Assert.That(error.Message, Does.Not.Contain("sensitive-response"));
        Assert.That(handler.Calls.Count(c => c.Method == HttpMethod.Post), Is.EqualTo(seedCalls));
        Assert.That(handler.Calls.Count(c => c.Method == HttpMethod.Delete), Is.EqualTo(1));
        Assert.That(handler.Calls[^1].Path, Is.EqualTo("/seed/" + session.PlayId));
    }

    [Test]
    public async Task QueryUsesRawResponseAndPlayHeader()
    {
        //Arrange
        var handler = new RecordingHandler();
        using var client = CreateClient(handler);

        //Act
        var code = await client.QueryAsync<EmailCodeResult>("query-play", "UserEmailTokenCodeQuery",
            new { email = "test@example.com", codeType = "EmailTwoFactor" });

        //Assert
        Assert.That(code.Found, Is.True);
        Assert.That(code.Code, Is.EqualTo("123456"));
        Assert.That(handler.Calls.Single().PlayId, Is.EqualTo("query-play"));
        Assert.That(handler.Calls.Single().Path, Is.EqualTo("/query"));
    }

    [Test]
    public async Task CleanupFailureIsReported()
    {
        //Arrange
        var handler = new RecordingHandler { FailCleanup = true };
        var session = new SeederSession(CreateClient(handler));
        await session.ProvisionAsync();

        //Act
        var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.DisposeAsync());

        //Assert
        Assert.That(error!.Message, Does.Contain("cleanup").And.Contain("HTTP 500"));
    }

    [Test]
    public void MalformedResponseDoesNotExposeResponseBody()
    {
        //Arrange
        var handler = new RecordingHandler { Malformed = true };
        using var client = CreateClient(handler);

        //Act
        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SeedAsync<UserSeedResult>("test-play", "SingleUserScene", new { }));

        //Assert
        Assert.That(error!.Message, Does.Contain("invalid response").And.Not.Contain("sensitive-response"));
    }

    private static SeederApiClient CreateClient(RecordingHandler handler) =>
        new(new SeederApiSettings("http://localhost/", "test-user", "test-password"), handler);

    private sealed record Call(HttpMethod Method, string Path, string PlayId, string Authorization, string Template, JsonElement Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];
        public string? FailTemplate { get; init; }
        public bool FailCleanup { get; init; }
        public bool Malformed { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? default : JsonSerializer.Deserialize<JsonElement>(await request.Content.ReadAsStringAsync(cancellationToken));
            var template = body.ValueKind == JsonValueKind.Object ? body.GetProperty("template").GetString()! : "";
            Calls.Add(new Call(request.Method, request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("X-Play-Id", out var values) ? values.Single() : "",
                request.Headers.Authorization!.ToString(), template, body));
            if (template == FailTemplate || (request.Method == HttpMethod.Delete && FailCleanup))
                return Response("sensitive-response", request.Method == HttpMethod.Delete ? HttpStatusCode.InternalServerError : HttpStatusCode.BadRequest);
            if (Malformed) return Response("sensitive-response");
            if (request.Method == HttpMethod.Delete) return Response("{}");
            if (template == "UserEmailTokenCodeQuery") return Response("{\"found\":true,\"code\":\"123456\"}");
            object result = template switch
            {
                "SingleUserScene" => new UserSeedResult("a6f4f254-3a44-4056-b5c3-dcc82aa8cf06", "test-key", 5000),
                "UserFolderScene" => new FolderSeedResult("test-folder"),
                "UserLoginCipherScene" => new CipherSeedResult("test-cipher"),
                _ => throw new InvalidOperationException("Unexpected template"),
            };
            var email = template == "SingleUserScene" ? body.GetProperty("arguments").GetProperty("email").GetString()! : "";
            return Response(JsonSerializer.Serialize(new { result, mangleMap = new Dictionary<string, string> { [email] = "mangled@example.com" } }, SeederApiClient.Json));
        }

        private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
