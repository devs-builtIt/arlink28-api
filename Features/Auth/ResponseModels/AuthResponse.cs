namespace Arlink28.Api.Features.Auth.ResponseModels;

public record AuthResponse(string AccessToken, string Role, string Username, DateTime ExpiresAt);
