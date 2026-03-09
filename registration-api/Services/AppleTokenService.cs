using System.Text.Json;

namespace RegistrationApi.Services;

public static class AppleTokenService
{
    public static string? ExtractEmailFromJwt(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        var payloadBase64 = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');

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
