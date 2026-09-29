using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers.Settings;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Arlink28.Api.Features.Shared.Services;

public class EmailService(
    IOptions<EmailSettings> emailSettings,
    IOptions<AppSettings> appSettings,
    ILogger<EmailService> logger) : IEmailService, IScoped
{
    private readonly EmailSettings _email = emailSettings.Value;
    private readonly AppSettings _app = appSettings.Value;

    public async Task SendInviteAsync(string toEmail, string plainToken, CancellationToken ct = default)
    {
        var link = $"{_app.FrontendBaseUrl}/invite/accept?token={Uri.EscapeDataString(plainToken)}";
        var body = $"You have been invited to ARLink28.\n\nSet up your account here (link expires in 48 hours):\n{link}";
        await SendAsync(toEmail, "You've been invited to ARLink28", body, ct);
    }

    public async Task SendPasswordResetAsync(string toEmail, string plainToken, CancellationToken ct = default)
    {
        var link = $"{_app.FrontendBaseUrl}/reset-password?token={Uri.EscapeDataString(plainToken)}";
        var body = $"A password reset was requested for your ARLink28 account.\n\nReset your password here (link expires in 1 hour):\n{link}\n\nIf you did not request this, ignore this email.";
        await SendAsync(toEmail, "Reset your ARLink28 password", body, ct);
    }

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_email.SmtpHost) || _email.SmtpHost.StartsWith("PLACEHOLDER"))
        {
            logger.LogWarning("Email SMTP not configured. Would send to {To}: {Subject} — {Body}", toEmail, subject, body);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_email.FromName, _email.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_email.SmtpHost, _email.SmtpPort, _email.EnableSsl, ct);
        if (!string.IsNullOrWhiteSpace(_email.SmtpUser))
            await client.AuthenticateAsync(_email.SmtpUser, _email.SmtpPass, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
