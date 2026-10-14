using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Helpers.Settings;

public class EnquirySettings
{
    public const string Section = "EnquirySettings";

    /// <summary>
    /// Who is emailed about every enquiry, whatever it is about: one address, or several separated by
    /// commas. Empty means nobody is added by this setting (enquiries are always saved and show in the admin).
    /// </summary>
    public string NotifyTo { get; set; } = string.Empty;

    /// <summary>
    /// Also emailed about enquiries that are about a booking: a package, flight, hotel or visa enquiry, a
    /// charter, or a quote request (types Package and Booking). Same format as <see cref="NotifyTo"/>.
    /// </summary>
    public string ReservationsTo { get; set; } = string.Empty;

    /// <summary>
    /// Also emailed about the general contact form: general questions, partnerships, careers and investors
    /// (every type except Package and Booking). Same format as <see cref="NotifyTo"/>.
    /// </summary>
    public string ContactTo { get; set; } = string.Empty;

    /// <summary>
    /// Everyone to email for an enquiry of this type, as one comma-separated list: <see cref="NotifyTo"/> plus the
    /// list for its kind, without duplicates (compared ignoring case). Empty means nobody is emailed.
    /// </summary>
    public string RecipientsFor(EnquiryType type)
    {
        var kindList = type is EnquiryType.Package or EnquiryType.Booking ? ReservationsTo : ContactTo;
        return string.Join(", ", Split(NotifyTo).Concat(Split(kindList)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Split(string? list) =>
        (list ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
