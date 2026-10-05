using System.Net.Http.Headers;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers.Settings;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Features.Shared.Services;

/// <summary>
/// Stores uploads in a public Supabase Storage bucket. Paths in the database stay <c>/media/{folder}/{name}</c>,
/// the same as on local disk, so switching provider needs no data change; the API redirects those URLs to the bucket.
/// </summary>
public class SupabaseMediaStorage(IOptions<MediaStorageSettings> options, HttpClient http) : IMediaStorage
{
    private readonly MediaStorageSettings _settings = options.Value;

    private string Base => _settings.SupabaseUrl.TrimEnd('/') + "/storage/v1/object";
    private string Prefix => _settings.PublicPath.TrimEnd('/') + "/";

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

        // Names are ours (a GUID), so a hostile filename never reaches the bucket.
        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        using var body = new StreamContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var request = Authorised(HttpMethod.Post, $"{Base}/{_settings.SupabaseBucket}/{key}");
        request.Content = body;

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Supabase Storage rejected the upload ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}");

        return new StoredImage(Prefix + key, contentType);
    }

    public void Delete(string publicPath)
    {
        var key = KeyOf(publicPath);
        if (key is null) return;

        // The interface is synchronous and deletes are rare admin actions, so blocking here is acceptable.
        using var request = Authorised(HttpMethod.Delete, $"{Base}/{_settings.SupabaseBucket}/{key}");
        using var response = http.Send(request);
        var status = (int)response.StatusCode;
        // A missing object is not an error (Supabase answers 400 or 404 for one).
        if (!response.IsSuccessStatusCode && status != 400 && status != 404)
            throw new InvalidOperationException($"Supabase Storage could not delete {key} ({status}).");
    }

    public async Task<byte[]?> ReadAsync(string publicPath, long maxBytes, CancellationToken ct = default)
    {
        var key = KeyOf(publicPath);
        if (key is null) return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, PublicUrl(key));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return null;
        if (response.Content.Headers.ContentLength is { } length && length > maxBytes) return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return bytes.Length > maxBytes ? null : bytes;
    }

    /// <summary>Where the browser should be sent for <c>/media/{path}</c>, or null for a path that isn't a plain bucket key.</summary>
    public string? PublicUrlFor(string path) => IsSafeKey(path) ? PublicUrl(path) : null;

    private string PublicUrl(string key) => $"{Base}/public/{_settings.SupabaseBucket}/{key}";

    private string? KeyOf(string publicPath)
    {
        if (!publicPath.StartsWith(Prefix, StringComparison.Ordinal)) return null; // a seeded /images/... file isn't ours
        var key = publicPath[Prefix.Length..];
        return IsSafeKey(key) ? key : null;
    }

    private static bool IsSafeKey(string key) =>
        key.Length > 0 && !key.Contains('\\') && !key.Split('/').Any(s => s is "" or "." or "..");

    private HttpRequestMessage Authorised(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SupabaseServiceKey);
        request.Headers.Add("apikey", _settings.SupabaseServiceKey);
        return request;
    }
}
