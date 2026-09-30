using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Enquiries.Services.Interfaces;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Features.Enquiries.Services;

public class EnquiryNotifier(
    ApplicationDbContext db,
    IEmailService email,
    IMediaStorage media,
    IOptions<EnquirySettings> settings,
    IOptions<AppSettings> app) : IEnquiryNotifier, IScoped
{
    /// <summary>A photo bigger than this is left out of the email: it travels inside every message.</summary>
    public const long MaxPhotoBytes = 400 * 1024;

    public async Task SendAsync(Guid enquiryId, CancellationToken ct = default)
    {
        var to = settings.Value.NotifyTo;
        if (string.IsNullOrWhiteSpace(to)) return;

        var e = await db.Enquiries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == enquiryId, ct);
        if (e is null) return;

        var (destination, party, photo) = await PackageDetailsAsync(e, ct);
        var notice = new EnquiryNotice(
            e.Reference,
            e.Type == EnquiryType.Package ? "package enquiry" : e.Type.ToString().ToLowerInvariant(),
            e.Name, e.Email, e.Phone, e.PackageTitle, e.CheckIn, e.Nights, TotalText(e), e.Subject, e.Message,
            $"{app.Value.FrontendBaseUrl.TrimEnd('/')}/admin/enquiries/{e.Id}",
            destination, party, e.CreatedAt, photo);
        await email.SendEnquiryNotificationAsync(to, notice, ct);
    }

    /// <summary>
    /// What makes the email read like a booking: where the package is, who it is packaged for, and its
    /// photo. With no photo (or one that is too big) the email simply has no picture.
    /// </summary>
    private async Task<(string? Destination, string? Party, NoticePhoto? Photo)> PackageDetailsAsync(Enquiry e, CancellationToken ct)
    {
        if (e.PackageId is not { } packageId) return (null, null, null);

        var package = await db.Packages.AsNoTracking()
            .Where(p => p.Id == packageId)
            .Select(p => new
            {
                Destination = p.Destination.Name,
                p.Adults,
                p.Children,
                Hero = p.Media
                    .Where(m => m.Role == MediaRole.Hero && m.VideoProvider == null)
                    .OrderBy(m => m.SortKey)
                    .Select(m => m.Path)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (package is null) return (null, null, null);

        var adults = $"{package.Adults} {(package.Adults == 1 ? "adult" : "adults")}";
        var party = package.Children > 0
            ? $"{adults}, {package.Children} {(package.Children == 1 ? "child" : "children")}"
            : adults;

        NoticePhoto? photo = null;
        if (package.Hero is not null && await media.ReadAsync(package.Hero, MaxPhotoBytes, ct) is { } bytes
            && media.SniffContentType(bytes.AsSpan(0, Math.Min(bytes.Length, 16))) is { } type)
            photo = new NoticePhoto(bytes, type);

        return (package.Destination, party, photo);
    }

    private static string? TotalText(Enquiry e)
    {
        if (e.QuotedTotalMinor is not { } minor || e.Currency is null) return null;
        var format = minor % 100 == 0 ? "N0" : "N2";
        return $"{e.Currency} {(minor / 100m).ToString(format, System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
