namespace Arlink28.Api.Helpers.Settings;

public class EmailSettings
{
    public const string Section = "EmailSettings";

    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
}
