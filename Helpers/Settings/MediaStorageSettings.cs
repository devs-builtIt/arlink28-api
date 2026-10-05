namespace Arlink28.Api.Helpers.Settings;

public class MediaStorageSettings
{
    public const string Section = "MediaStorage";

    /// <summary>"LocalDisk" (default) or "Supabase". Local disk does not survive a redeploy on Render.</summary>
    public string Provider { get; set; } = "LocalDisk";

    /// <summary>Supabase provider: project URL, e.g. https://abcdefgh.supabase.co.</summary>
    public string SupabaseUrl { get; set; } = "";

    /// <summary>Supabase provider: name of a PUBLIC Storage bucket.</summary>
    public string SupabaseBucket { get; set; } = "media";

    /// <summary>Supabase provider: the service_role key. A server-only secret; set it as an environment variable.</summary>
    public string SupabaseServiceKey { get; set; } = "";

    /// <summary>Folder uploads are written to. Relative paths resolve against the content root.</summary>
    public string RootPath { get; set; } = "local-storage/media";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Photos accepted in one upload request.</summary>
    public int MaxFilesPerRequest { get; set; } = 20;

    /// <summary>URL prefix the files are served under (and stored in `PackageMedia.Path`).</summary>
    public string PublicPath { get; set; } = "/media";
}
