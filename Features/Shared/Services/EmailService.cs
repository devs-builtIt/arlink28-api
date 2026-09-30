using System.Globalization;
using System.Text;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
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

    public async Task SendEnquiryNotificationAsync(string toEmail, EnquiryNotice notice, CancellationToken ct = default)
    {
        var about = notice.PackageTitle ?? notice.Subject ?? notice.Kind;
        await SendAsync(toEmail, $"New enquiry {notice.Reference}: {about}", EnquiryBody(notice), ct,
            replyTo: notice.GuestEmail, replyToName: notice.GuestName,
            html: (logoCid, photoCid) => EnquiryEmailTemplate.Html(
                notice, $"cid:{logoCid}", photoCid is null ? null : $"cid:{photoCid}"),
            photo: notice.Photo);
    }

    /// <summary>The mark at the top of emails, read from inside the assembly.</summary>
    private static byte[] LogoBytes()
    {
        using var stream = typeof(EmailService).Assembly.GetManifestResourceStream("Arlink28.Api.Assets.email-logo.png")
            ?? throw new InvalidOperationException("The email logo is missing from the build.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public static string EnquiryBody(EnquiryNotice n)
    {
        var text = new StringBuilder();
        text.AppendLine($"New enquiry {n.Reference} ({n.Kind}).");
        text.AppendLine();
        text.AppendLine($"Guest:  {n.GuestName}");
        text.AppendLine($"Email:  {n.GuestEmail}");
        if (n.Phone is not null) text.AppendLine($"Phone:  {n.Phone}");
        if (n.PackageTitle is not null)
        {
            text.AppendLine();
            text.AppendLine($"Package:   {n.PackageTitle}{(n.Destination is null ? "" : $" ({n.Destination})")}");
            if (n.Party is not null) text.AppendLine($"Party:     {n.Party}");
            if (n.CheckIn is { } checkIn)
                text.AppendLine($"Check-in:  {checkIn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}");
            if (n.CheckIn is { } from && n.Nights is { } stay)
                text.AppendLine($"Check-out: {from.AddDays(stay).ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}");
            if (n.Nights is { } nights) text.AppendLine($"Nights:    {nights}");
            text.AppendLine($"Quoted:    {n.QuotedTotal ?? "no online price for these dates"}");
        }
        if (n.Subject is not null)
        {
            text.AppendLine();
            text.AppendLine($"Subject: {n.Subject}");
        }
        if (n.Message is not null)
        {
            text.AppendLine();
            text.AppendLine(n.Message);
        }
        text.AppendLine();
        text.AppendLine($"Open it in the admin: {n.AdminLink}");
        text.AppendLine("Reply to this email to answer the guest directly.");
        return text.ToString();
    }

    private async Task SendAsync(
        string toEmail, string subject, string body, CancellationToken ct,
        string? replyTo = null, string? replyToName = null,
        Func<string, string?, string>? html = null, NoticePhoto? photo = null)
    {
        if (string.IsNullOrWhiteSpace(_email.SmtpHost) || _email.SmtpHost.StartsWith("PLACEHOLDER"))
        {
            logger.LogWarning("Email SMTP not configured. Would send to {To}: {Subject} — {Body}", toEmail, subject, body);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_email.FromName, _email.FromEmail));
        foreach (var to in toEmail.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(MailboxAddress.Parse(to));
        // The address came from a guest's form: a malformed one is just left off rather than failing the email.
        if (replyTo is not null && MailboxAddress.TryParse(replyTo, out var reply))
        {
            reply.Name = replyToName;
            message.ReplyTo.Add(reply);
        }
        message.Subject = subject.ReplaceLineEndings(" ");
        if (html is null)
        {
            message.Body = new TextPart("plain") { Text = body };
        }
        else
        {
            // Both versions go out: clients that can't show HTML fall back to the plain text.
            var parts = new BodyBuilder { TextBody = body };
            var logo = parts.LinkedResources.Add("arlink28.png", LogoBytes(), ContentType.Parse("image/png"));
            logo.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId();
            string? photoCid = null;
            if (photo is not null)
            {
                var extension = photo.ContentType == "image/png" ? "png" : photo.ContentType == "image/webp" ? "webp" : "jpg";
                var image = parts.LinkedResources.Add($"package.{extension}", photo.Bytes, ContentType.Parse(photo.ContentType));
                image.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId();
                photoCid = image.ContentId;
            }
            parts.HtmlBody = html(logo.ContentId, photoCid);
            message.Body = parts.ToMessageBody();
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(_email.SmtpHost, _email.SmtpPort, SecurityFor(_email), ct);
        if (!string.IsNullOrWhiteSpace(_email.SmtpUser))
            await client.AuthenticateAsync(_email.SmtpUser, _email.SmtpPass, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    /// <summary>Port 465 is encrypted from the first byte; any other port starts plain and upgrades (STARTTLS).</summary>
    public static SecureSocketOptions SecurityFor(EmailSettings settings) =>
        !settings.EnableSsl ? SecureSocketOptions.None
        : settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect
        : SecureSocketOptions.StartTls;
}
