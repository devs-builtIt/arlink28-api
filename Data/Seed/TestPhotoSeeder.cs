using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Data.Seed;

/// <summary>
/// TEST PHOTOS, for while packages have no real photography. Downloads a few stock photos (Unsplash)
/// into media storage and adds them to each package that has no uploaded photo: the first as its
/// Hero, the rest as Gallery. Run with <c>dotnet run -- seed-photos</c> (see Program.cs).
///
/// - A poster that came with the site (path outside /media/) stays, and becomes the package's Poster.
/// - Everything added is captioned <see cref="Marker"/>, so <see cref="UndoAsync"/> removes exactly
///   that and puts the poster back as the Hero.
/// - A package that already has an uploaded photo is left alone, so re-running is safe.
/// </summary>
public class TestPhotoSeeder(ApplicationDbContext db, IMediaStorage storage, HttpClient http)
{
    public const string Marker = "Test photo";
    public const int Width = 1600;
    public const int Height = 1067;

    private record Photo(string Id, string Alt);

    private static readonly Photo Giraffe = new("1554490828-442467b562dd", "A giraffe on the open plains");
    private static readonly Photo GiraffeHerd = new("1632518876532-8a2a3c735705", "Giraffes crossing dry grassland");
    private static readonly Photo GiraffePool = new("1515914560649-8fe5d631aa62", "A guest watching a giraffe from a lodge pool");
    private static readonly Photo Lion = new("1754424612239-677364e244d6", "A lion in tall grass");
    private static readonly Photo Elephants = new("1592670130129-4388cdb9d76e", "Elephants crossing a dirt road");
    private static readonly Photo Savanna = new("1547471080-7cc2caa01a7e", "An acacia tree at sunset on the savanna");
    private static readonly Photo Lodge = new("1779216175784-a67b6da108bb", "The open-air lounge of a thatched lodge");
    private static readonly Photo LodgePool = new("1781039869379-5561fe260d26", "A safari lodge and its pool at dusk");
    private static readonly Photo LodgeDining = new("1722645390607-9f69ce4cfadf", "A lodge dining room with a timber ceiling");
    private static readonly Photo Beach = new("1607444807093-eefb769187da", "A beach and turquoise water from above");
    private static readonly Photo Palms = new("1665449417444-fe7fec4b7425", "Palm trees on a beach");

    // The same sets the website uses for its test photos (apps/web/utils/testPhotos.ts).
    private static readonly Dictionary<string, Photo[]> Sets = new()
    {
        ["nairobi"] = [Giraffe, GiraffeHerd, GiraffePool, LodgeDining, Lodge, Savanna],
        ["masai-mara"] = [Lion, Savanna, Lodge, GiraffeHerd, LodgePool, Elephants],
        ["samburu"] = [Elephants, Savanna, Lion, LodgePool, Lodge, Giraffe],
        ["zanzibar"] = [Beach, Palms, LodgePool, Lodge, LodgeDining, Savanna],
    };
    private static readonly Photo[] Fallback = [Savanna, GiraffeHerd, Lodge, Lion, LodgePool, Elephants];

    private static string UrlOf(Photo p) =>
        $"https://images.unsplash.com/photo-{p.Id}?auto=format&fit=crop&w={Width}&h={Height}&q=75";

    private static bool IsUploaded(string path) => path.StartsWith("/media/", StringComparison.Ordinal);

    /// <summary>The photos for a package: its destination's set, started at a different photo for each package.</summary>
    public static IReadOnlyList<(string Url, string Alt)> Pick(string packageSlug, string destinationSlug, int count)
    {
        var set = Sets.GetValueOrDefault(destinationSlug, Fallback);
        uint hash = 7;
        foreach (var c in packageSlug) hash = unchecked(hash * 31 + c);
        var start = (int)(hash % (uint)set.Length);
        return Enumerable.Range(0, Math.Min(count, set.Length))
            .Select(i => set[(start + i) % set.Length])
            .Select(p => (UrlOf(p), p.Alt))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> SeedAsync(int perPackage, bool dryRun, CancellationToken ct = default)
    {
        var lines = new List<string>();
        var downloads = new Dictionary<string, byte[]>();
        var packages = await db.Packages.Include(p => p.Destination).Include(p => p.Media)
            .OrderBy(p => p.Slug).ToListAsync(ct);

        foreach (var package in packages)
        {
            if (package.Media.Any(m => IsUploaded(m.Path)))
            {
                lines.Add($"skip   {package.Slug}: already has uploaded photos");
                continue;
            }

            var picks = Pick(package.Slug, package.Destination.Slug, perPackage);
            var poster = package.Media.Count(m => m.Role == MediaRole.Hero && !IsUploaded(m.Path));
            lines.Add($"{(dryRun ? "would " : "add   ")} {package.Slug}: {picks.Count} photos"
                      + (poster > 0 ? " (the poster becomes its Poster)" : ""));
            if (dryRun) continue;

            var written = new List<string>();
            try
            {
                foreach (var poster1 in package.Media.Where(m => m.Role == MediaRole.Hero && !IsUploaded(m.Path)))
                    poster1.Role = MediaRole.Poster;

                var sortKey = package.Media.Count == 0 ? 0 : package.Media.Max(m => m.SortKey) + 1;
                for (var i = 0; i < picks.Count; i++)
                {
                    var (url, alt) = picks[i];
                    if (!downloads.TryGetValue(url, out var bytes))
                        downloads[url] = bytes = await http.GetByteArrayAsync(url, ct);

                    var contentType = storage.SniffContentType(bytes.AsSpan(0, Math.Min(12, bytes.Length)))
                        ?? throw new InvalidOperationException($"{url} did not return a JPEG, PNG or WebP image.");
                    await using var stream = new MemoryStream(bytes);
                    var stored = await storage.SaveAsync($"packages/{package.Id:N}", stream, contentType, ct);
                    written.Add(stored.Path);

                    db.PackageMedia.Add(new PackageMedia
                    {
                        PackageId = package.Id,
                        Role = i == 0 ? MediaRole.Hero : MediaRole.Gallery,
                        Path = stored.Path,
                        Alt = alt,
                        Caption = Marker,
                        Width = Width,
                        Height = Height,
                        SortKey = sortKey + i,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Nothing half-added: forget the rows and remove the files this package wrote.
                db.ChangeTracker.Clear();
                foreach (var path in written) storage.Delete(path);
                throw;
            }
        }
        return lines;
    }

    /// <summary>Removes the test photos and files, and puts each poster back as its package's Hero.</summary>
    public async Task<IReadOnlyList<string>> UndoAsync(bool dryRun, CancellationToken ct = default)
    {
        var lines = new List<string>();
        var packages = await db.Packages.Include(p => p.Media)
            .Where(p => p.Media.Any(m => m.Caption == Marker)).OrderBy(p => p.Slug).ToListAsync(ct);

        foreach (var package in packages)
        {
            var test = package.Media.Where(m => m.Caption == Marker && IsUploaded(m.Path)).ToList();
            lines.Add($"{(dryRun ? "would " : "remove")} {package.Slug}: {test.Count} test photos");
            if (dryRun) continue;

            db.PackageMedia.RemoveRange(test);
            var remaining = package.Media.Except(test).ToList();
            if (!remaining.Any(m => m.Role == MediaRole.Hero))
            {
                var poster = remaining.Where(m => m.Role == MediaRole.Poster && !IsUploaded(m.Path)).OrderBy(m => m.SortKey).FirstOrDefault();
                if (poster is not null) poster.Role = MediaRole.Hero;
            }
            await db.SaveChangesAsync(ct);
            foreach (var m in test) storage.Delete(m.Path); // after the rows are gone
        }
        return lines;
    }
}
