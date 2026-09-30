namespace Arlink28.Api.Helpers.Settings;

public class EnquirySettings
{
    public const string Section = "EnquirySettings";

    /// <summary>
    /// Who is emailed when a guest sends an enquiry: one address, or several separated by commas.
    /// Empty means no email is sent (enquiries are still saved and show in the admin).
    /// </summary>
    public string NotifyTo { get; set; } = string.Empty;
}
