using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers.Settings;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Features.Shared.Services;

public class LocalDiskMediaStorage(IOptions<MediaStorageSettings> options, IWebHostEnvironment env) : IMediaStorage, ISingleton
{
    private readonly MediaStorageSettings _settings = options.Value;

    public string RootFullPath => Path.GetFullPath(_settings.RootPath, env.ContentRootPath);

    public string? SniffContentType(ReadOnlySpan<byte> h)
    {
        string? type = null;
        if (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            type = "image/jpeg";
        else if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            type = "image/png";
        else if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8))
            type = "image/webp";

        return type is not null && _settings.AllowedContentTypes.Contains(type) ? type : null;
    }

    public async Task<StoredImage> SaveAsync(string folder, Stream content, string contentType, CancellationToken ct = default)
    {
        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException($"Unsupported content type {contentType}.", nameof(contentType)),
        };

        // Names are ours (a GUID), so a hostile filename never reaches the disk.
        var name = $"{Guid.NewGuid():N}{extension}";
        var directory = Path.Combine(RootFullPath, folder);
        Directory.CreateDirectory(directory);

        await using (var file = File.Create(Path.Combine(directory, name)))
            await content.CopyToAsync(file, ct);

        return new StoredImage($"{_settings.PublicPath.TrimEnd('/')}/{folder}/{name}", contentType);
    }

    public void Delete(string publicPath)
    {
        var full = ResolveOwned(publicPath);
        if (full is not null && File.Exists(full)) File.Delete(full);
    }

    public async Task<byte[]?> ReadAsync(string publicPath, long maxBytes, CancellationToken ct = default)
    {
        var full = ResolveOwned(publicPath);
        if (full is null || !File.Exists(full) || new FileInfo(full).Length > maxBytes) return null;
        return await File.ReadAllBytesAsync(full, ct);
    }

    /// <summary>The file behind a public path, or null when it isn't one of ours or climbs out of the media folder.</summary>
    private string? ResolveOwned(string publicPath)
    {
        var prefix = _settings.PublicPath.TrimEnd('/') + "/";
        if (!publicPath.StartsWith(prefix, StringComparison.Ordinal)) return null; // a seeded /images/... file isn't ours

        var full = Path.GetFullPath(Path.Combine(RootFullPath, publicPath[prefix.Length..]));
        return full.StartsWith(RootFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }
}
