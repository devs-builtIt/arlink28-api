namespace Arlink28.Api.Features.UserManagement.ResponseModels;

public record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt
);
