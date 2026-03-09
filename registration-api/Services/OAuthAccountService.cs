using System.Security.Cryptography;
using System.Text;

namespace RegistrationApi.Services;

public sealed class OAuthAccountService
{
    private readonly AccountRepository _repo;
    private readonly string _oauthSecret;

    public OAuthAccountService(AccountRepository repo, string oauthSecret)
    {
        _repo = repo;
        _oauthSecret = oauthSecret;
    }

    public async Task<(string Username, string Password)> GetOrCreateAccountAsync(string email)
    {
        var password = DerivePassword(email);
        var existing = await _repo.FindByEmailAsync(email);

        if (existing != null)
            return (existing, password);

        var username = GenerateUsername(email);

        while (await _repo.ExistsAsync(username))
            username = GenerateUsername(email + Guid.NewGuid().ToString("N")[..4]);

        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
        await _repo.CreateAccountAsync(username, hash, email);

        return (username, password);
    }

    private string DerivePassword(string email)
    {
        var key = Encoding.UTF8.GetBytes(_oauthSecret);
        var data = Encoding.UTF8.GetBytes(email.ToLowerInvariant());
        var hmac = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hmac)[..20];
    }

    private static string GenerateUsername(string email)
    {
        var localPart = email.Split('@')[0];
        // Keep only alphanumeric, truncate to 10 chars (OpenMU limit)
        var clean = new string(localPart.Where(char.IsLetterOrDigit).ToArray());
        return clean.Length > 10 ? clean[..10] : clean;
    }
}
