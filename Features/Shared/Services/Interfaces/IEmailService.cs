namespace Arlink28.Api.Features.Shared.Services.Interfaces;

public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string plainToken, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string plainToken, CancellationToken ct = default);
}
