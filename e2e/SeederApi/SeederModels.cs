namespace e2e.SeederApi;

public sealed record SeededAccount(string Email, string Password, string UserId);
public sealed record SeederApiSettings(string SeederApiUrl, string SeederApiUsername, string SeederApiPassword);
public sealed record SeedResponse<T>(T Result, Dictionary<string, string?> MangleMap);
public sealed record UserSeedResult(string UserId, string DecryptedKeyB64, int KdfIterations);
public sealed record FolderSeedResult(string FolderId);
public sealed record CipherSeedResult(string CipherId);
public sealed record EmailCodeResult(bool Found, string? Code);

public sealed record CreateUserRequest(string Email, string Password, bool EmailVerified = true);
public sealed record CreateFolderRequest(string UserId, string UserKeyB64, string FolderName);
public sealed record CreateLoginRequest(string UserId, string UserKeyB64, string Name, string? FolderId = null,
    string Username = "e2e", string Uri = "https://example.com");
public sealed record EmailCodeQuery(string Email, string CodeType = "EmailTwoFactor");
