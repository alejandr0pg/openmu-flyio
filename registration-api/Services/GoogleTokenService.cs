using System.Net.Http.Json;
using System.Text.Json;

namespace RegistrationApi.Services;

public sealed class GoogleTokenService
{
    private static readonly HttpClient Http = new();
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    public GoogleTokenService(string clientId, string clientSecret, string redirectUri)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _redirectUri = redirectUri;
    }

    public async Task<string?> ExchangeCodeForEmailAsync(string authCode)
    {
        var payload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = authCode,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = _redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await Http.PostAsync(TokenEndpoint, payload);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (!json.TryGetProperty("id_token", out var idTokenProp))
            return null;

        return ExtractEmailFromIdToken(idTokenProp.GetString()!);
    }

    private static string? ExtractEmailFromIdToken(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length < 2) return null;

        var payloadBase64 = parts[1];
        // Fix base64url padding
        payloadBase64 = payloadBase64.Replace('-', '+').Replace('_', '/');
        switch (payloadBase64.Length % 4)
        {
            case 2: payloadBase64 += "=="; break;
            case 3: payloadBase64 += "="; break;
        }

        var payloadBytes = Convert.FromBase64String(payloadBase64);
        var payload = JsonSerializer.Deserialize<JsonElement>(payloadBytes);

        return payload.TryGetProperty("email", out var email)
            ? email.GetString()
            : null;
    }
}
