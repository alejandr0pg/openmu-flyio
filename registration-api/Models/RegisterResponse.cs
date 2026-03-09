using System.Text.Json.Serialization;

namespace RegistrationApi.Models;

public sealed record RegisterResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message);
