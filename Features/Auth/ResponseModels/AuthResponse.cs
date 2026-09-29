using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.Auth.ResponseModels;

public record AuthResponse(string AccessToken, StaffRole Role, string Username, DateTime ExpiresAt);
