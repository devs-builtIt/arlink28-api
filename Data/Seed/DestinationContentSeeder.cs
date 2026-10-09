using System.Text.Json;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Data.Seed;

public record PhotoContent(string File, string Alt, string Credit, string? Source = null);

public record AttractionContent(string Name, string Summary);

public record DestinationContent(
    string Slug,
    string? Tagline = null,
    string? Summary = null,
    string? Description = null,
    string? BestTimeToVisit = null,
    double? Latitude = null,
    double? Longitude = null,
    PhotoContent? Photo = null,
    List<AttractionContent>? Attractions = null);

public record DestinationContentFile(List<DestinationContent> Destinations);

/// <summary>
/// The first write-ups for the African destinations (Data/Seed/destination-content.json), loaded with
/// <c>dotnet run -- seed-destinations</c> (see Program.cs).
///
/// - It only FILLS: a field that already has text, a destination that already has attractions, and a
///   destination that already has a hero photo are all left exactly as staff left them. Re-running is safe.
/// - It never publishes. Status and PublishedAt are not touched; staff review the text in the admin and publish.
/// - Photos are read from a folder on the machine running it (<c>--photos=</c>), and saved through the
///   configured media storage, so they land wherever uploads from the admin land.
/// </summary>
public class DestinationContentSeeder(ApplicationDbContext db, IMediaStorage storage, IOptions<MediaStorageSettings> media)
{
    private const string ResourceName = "Arlink28.Api.Data.Seed.destination-content.json";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The write-ups shipped with the API.</summary>
    public static IReadOnlyList<DestinationContent> LoadContent()
    {
        using var stream = typeof(DestinationContentSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded content '{ResourceName}' is missing.");
        return JsonSerializer.Deserialize<DestinationContentFile>(stream, Json)?.Destinations
            ?? throw new InvalidOperationException("The destination content file is empty.");
    }

    /// <param name="photosDirectory">Folder holding the photo files named in the content, or null to skip photos.</param>
    /// <param name="dryRun">Report what would change, and change nothing (no database writes, no uploads).</param>
    public async Task<IReadOnlyList<string>> SeedAsync(
        string? photosDirectory, bool dryRun, CancellationToken ct = default) =>
        await SeedAsync(LoadContent(), photosDirectory, dryRun, ct);

    public async Task<IReadOnlyList<string>> SeedAsync(
        IReadOnlyList<DestinationContent> content, string? photosDirectory, bool dryRun, CancellationToken ct = default)
    {
        var lines = new List<string>();
        var uploaded = new List<string>();

        try
        {
            foreach (var item in content)
            {
                var d = await db.Destinations.Include(x => x.Attractions).FirstOrDefaultAsync(x => x.Slug == item.Slug, ct);
                if (d is null)
                {
                    lines.Add($"missing {item.Slug}: no such destination (apply migration 004, then run seed-catalogue)");
                    continue;
                }

                var filled = new List<string>();
                var notes = new List<string>();

                Fill(d.Tagline, item.Tagline, v => d.Tagline = v, "tagline", filled);
                Fill(d.Summary, item.Summary, v => d.Summary = v, "summary", filled);
                Fill(d.Description, item.Description, v => d.Description = v, "description", filled);
                Fill(d.BestTimeToVisit, item.BestTimeToVisit, v => d.BestTimeToVisit = v, "best time to visit", filled);
                if (d.Latitude is null && item.Latitude is { } lat && d.Longitude is null && item.Longitude is { } lng)
                {
                    d.Latitude = lat;
                    d.Longitude = lng;
                    filled.Add("coordinates");
                }

                if (item.Attractions is { Count: > 0 } attractions)
                {
                    if (d.Attractions.Count == 0)
                    {
                        for (var i = 0; i < attractions.Count; i++)
                            db.DestinationAttractions.Add(new DestinationAttraction
                            {
                                Id = Guid.NewGuid(),
                                DestinationId = d.Id,
                                Name = attractions[i].Name,
                                Summary = attractions[i].Summary,
                                SortOrder = i,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                            });
                        filled.Add($"{attractions.Count} attractions");
                    }
                    else notes.Add($"kept its {d.Attractions.Count} attractions");
                }

                if (item.Photo is { } photo && d.HeroPath is null)
                {
                    var outcome = await TryAddHeroAsync(d, photo, photosDirectory, dryRun, uploaded, ct);
                    if (outcome.Added) filled.Add("hero photo");
                    else notes.Add(outcome.Reason!);
                }
                else if (item.Photo is not null) notes.Add("kept its hero photo");

                if (filled.Count > 0)
                {
                    d.UpdatedAt = DateTime.UtcNow;
                    db.AuditLogs.Add(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        Action = "DestinationContentSeeded",
                        EntityType = nameof(Destination),
                        EntityId = d.Id.ToString(),
                        After = JsonSerializer.Serialize(new { fields = filled }),
                        CreatedAt = DateTime.UtcNow,
                    });
                }

                var detail = string.Join("; ", new[] { filled.Count > 0 ? "filled " + string.Join(", ", filled) : null }
                    .Concat(notes).Where(x => x is not null));
                lines.Add($"{(filled.Count > 0 ? (dryRun ? "would  " : "filled ") : "keep   ")}{item.Slug}{(detail.Length > 0 ? ": " + detail : "")}");
            }

            if (dryRun) db.ChangeTracker.Clear();
            else await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Nothing was saved, so nothing uploaded may stay behind.
            foreach (var path in uploaded) storage.Delete(path);
            throw;
        }

        return lines;
    }

    private static void Fill(string? current, string? wanted, Action<string> set, string name, List<string> filled)
    {
        if (!string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(wanted)) return;
        set(wanted.Trim());
        filled.Add(name);
    }

    private async Task<(bool Added, string? Reason)> TryAddHeroAsync(
        Destination d, PhotoContent photo, string? directory, bool dryRun, List<string> uploaded, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(directory)) return (false, "no hero photo: pass --photos=<folder> to add it");

        var file = Path.Combine(directory, photo.File);
        if (!File.Exists(file)) return (false, $"no hero photo: {photo.File} is not in {directory}");

        var bytes = await File.ReadAllBytesAsync(file, ct);
        var limit = media.Value.MaxFileSizeBytes;
        if (bytes.Length > limit) return (false, $"no hero photo: {photo.File} is larger than {limit / (1024 * 1024)} MB");
        var contentType = storage.SniffContentType(bytes.AsSpan(0, Math.Min(12, bytes.Length)));
        if (contentType is null) return (false, $"no hero photo: {photo.File} is not a JPEG, PNG or WebP image");

        if (!dryRun)
        {
            await using var stream = new MemoryStream(bytes);
            var stored = await storage.SaveAsync($"destinations/{d.Id:N}", stream, contentType, ct);
            uploaded.Add(stored.Path);
            d.HeroPath = stored.Path;
        }
        d.HeroAlt = photo.Alt;
        d.HeroCredit = photo.Credit;
        return (true, null);
    }
}
