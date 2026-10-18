using System.Security.Cryptography;

namespace e2e.SeederApi;

/// <summary>Owns all server data created for a single test, including partially provisioned data.</summary>
public sealed class SeederSession(SeederApiClient client) : IAsyncDisposable
{
    public string PlayId { get; } = "e2e-" + Guid.NewGuid().ToString("N");
    public SeededAccount Account { get; private set; } = null!;
    public int KdfIterations { get; private set; }
    private string? userKey;
    private bool disposed;

    public Task<SeedResponse<T>> SeedAsync<T>(string template, object arguments, CancellationToken cancellationToken = default) =>
        client.SeedAsync<T>(PlayId, template, arguments, cancellationToken);

    public Task<T> QueryAsync<T>(string template, object arguments, CancellationToken cancellationToken = default) =>
        client.QueryAsync<T>(PlayId, template, arguments, cancellationToken);

    public async Task ProvisionAsync()
    {
        try { await ProvisionCoreAsync(); }
        catch (Exception setupError)
        {
            try { await DisposeAsync(); }
            catch (Exception cleanupError) { throw new AggregateException("Provisioning and play cleanup failed.", setupError, cleanupError); }
            throw;
        }
    }

    private async Task ProvisionCoreAsync()
    {
        var email = $"{PlayId}@example.com";
        var password = "E2e-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "!";
        var seed = await SeedAsync<UserSeedResult>("SingleUserScene",
            new CreateUserRequest(email, password));
        if (seed.Result is null || seed.MangleMap is null || seed.Result.KdfIterations <= 0 ||
            !Guid.TryParse(seed.Result.UserId, out _) || string.IsNullOrEmpty(seed.Result.DecryptedKeyB64))
            throw new InvalidOperationException("SingleUserScene returned incomplete account data.");
        var actualEmail = seed.MangleMap.GetValueOrDefault(email) ?? email;
        Account = new SeededAccount(actualEmail, password, seed.Result.UserId);
        KdfIterations = seed.Result.KdfIterations;
        userKey = seed.Result.DecryptedKeyB64;
        var folder = await CreateFolderAsync("E2E folder");
        await CreateLoginAsync("E2E login", folder);
    }

    public async Task<string> CreateFolderAsync(string name)
    {
        var response = await SeedAsync<FolderSeedResult>("UserFolderScene",
            new CreateFolderRequest(Account.UserId, userKey!, name));
        return response.Result.FolderId;
    }

    public async Task<string> CreateLoginAsync(string name, string? folderId = null)
    {
        var response = await SeedAsync<CipherSeedResult>("UserLoginCipherScene",
            new CreateLoginRequest(Account.UserId, userKey!, name, folderId));
        return response.Result.CipherId;
    }

    public async Task<string> GetEmailCodeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var result = await QueryAsync<EmailCodeResult>("UserEmailTokenCodeQuery",
                    new EmailCodeQuery(Account.Email), timeout.Token);
                if (result.Found && !string.IsNullOrEmpty(result.Code))
                    return result.Code;
                await Task.Delay(100, timeout.Token);
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Seeder API did not find a newly issued Email two-factor code within 15 seconds.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        try { await client.DeleteAsync(PlayId); }
        finally { userKey = null; client.Dispose(); }
    }
}
