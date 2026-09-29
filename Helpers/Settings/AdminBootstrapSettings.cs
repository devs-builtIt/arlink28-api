namespace Arlink28.Api.Helpers.Settings;

public class AdminBootstrapSettings
{
    public const string Section = "AdminBootstrap";

    public bool Enabled { get; set; } = false;
    public string Username { get; set; } = "superadmin";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
