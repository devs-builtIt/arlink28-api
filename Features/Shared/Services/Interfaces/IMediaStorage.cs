namespace Arlink28.Api.Features.Shared.Services.Interfaces;

public record StoredImage(string Path, string ContentType);

public interface IMediaStorage
{
    /// <summary>
    /// Identifies an image by its first bytes, never by its filename or the client's Content-Type.
    /// Returns null unless it's a JPEG, PNG or WebP that the settings allow.
    /// </summary>
    string? SniffContentType(ReadOnlySpan<byte> header);

    /// <summary>Writes the image under <paramref name="folder"/> and returns its public path.</summary>
    Task<StoredImage> SaveAsync(string folder, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// The bytes of a stored image, or null when it is missing, isn't one of ours, or is larger than
    /// <paramref name="maxBytes"/>.
    /// </summary>
    Task<byte[]?> ReadAsync(string publicPath, long maxBytes, CancellationToken ct = default);

    /// <summary>Deletes the file behind a public path. A missing file is not an error.</summary>
    void Delete(string publicPath);
}
