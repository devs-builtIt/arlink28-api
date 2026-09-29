using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.Auth.ResponseModels;

/// <summary>The signed-in staff member, read fresh from the database. ExpiresAt is the token's expiry.</summary>
public record MeResponse(Guid Id, string Username, string Email, StaffRole Role, DateTime ExpiresAt);
