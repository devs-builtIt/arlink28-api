namespace Arlink28.Api.Features.Shared.Services.Interfaces;

/// <summary>A small picture sent inside the email (the package's hero photo).</summary>
public record NoticePhoto(byte[] Bytes, string ContentType);

/// <summary>What staff are told about a new enquiry. Plain values, so the email service knows nothing of entities.</summary>
public record EnquiryNotice(
    string Reference,
    string Kind,
    string GuestName,
    string GuestEmail,
    string? Phone,
    string? PackageTitle,
    DateOnly? CheckIn,
    int? Nights,
    string? QuotedTotal,
    string? Subject,
    string? Message,
    string AdminLink,
    string? Destination = null,
    string? Party = null,
    DateTime? ReceivedAt = null,
    NoticePhoto? Photo = null
);

public interface IEmailService
{
    Task SendInviteAsync(string toEmail, string plainToken, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string plainToken, CancellationToken ct = default);

    /// <param name="toEmail">One address, or several separated by commas. Replying goes to the guest.</param>
    Task SendEnquiryNotificationAsync(string toEmail, EnquiryNotice notice, CancellationToken ct = default);
}
