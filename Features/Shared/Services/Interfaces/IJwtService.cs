using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.Shared.Services.Interfaces;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) IssueToken(Staff staff);
}
