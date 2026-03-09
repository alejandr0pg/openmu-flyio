using System.Text.Json.Serialization;

namespace RegistrationApi.Models;

public sealed record RegisterRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);
