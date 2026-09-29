using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.UserManagement.ResponseModels;

public record UserResponse(
    Guid Id,
    string Username,
    string Email,
    StaffRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt
);
