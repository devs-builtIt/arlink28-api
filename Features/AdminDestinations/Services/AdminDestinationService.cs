using System.Text.Json;
using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminDestinations.RequestModels;
using Arlink28.Api.Features.AdminDestinations.ResponseModels;
using Arlink28.Api.Features.AdminDestinations.Services.Interfaces;
using Arlink28.Api.Features.AdminPackages.Services;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Features.Catalogue.ResponseModels;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers;
using Arlink28.Api.Helpers.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Features.AdminDestinations.Services;

public class AdminDestinationService(
    ApplicationDbContext db,
    IMediaStorage storage,
    IOptions<MediaStorageSettings> mediaOptions) : IAdminDestinationService, IScoped
{
    private const int MaxAttractions = 50;
    private readonly MediaStorageSettings _media = mediaOptions.Value;

    public async Task<AdminDestinationListResponse> ListAsync(
        string? status, string? search, string? country, string? kind, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var matching = db.Destinations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            matching = matching.Where(d => d.Name.ToLower().Contains(term) || d.Slug.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(country))
        {
            var code = country.Trim().ToUpperInvariant();
            matching = matching.Where(d => d.Country == code);
        }
        if (Enum.TryParse<DestinationKind>(kind, ignoreCase: true, out var parsedKind))
            matching = matching.Where(d => d.Kind == parsedKind);

        // The tabs count every status for the search and filters, whichever tab is open.
        var byStatus = await matching.GroupBy(d => d.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int CountOf(DestinationStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        var counts = new DestinationStatusCounts(byStatus.Sum(x => x.Count), CountOf(DestinationStatus.Draft), CountOf(DestinationStatus.Published));

        var filtered = Enum.TryParse<DestinationStatus>(status, ignoreCase: true, out var parsedStatus)
            ? matching.Where(d => d.Status == parsedStatus)
            : matching;
        var total = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderBy(d => d.Country).ThenBy(d => d.Kind).ThenBy(d => d.SortOrder).ThenBy(d => d.Name).ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new AdminDestinationSummary(
                d.Id, d.Slug, d.Name, d.Country, d.Kind.ToString(), d.Status.ToString(),
                d.Parent == null ? null : d.Parent.Name, d.HeroPath,
                d.Places.Count, d.Attractions.Count, d.UpdatedAt))
            .ToListAsync(ct);
        return new AdminDestinationListResponse(items, total, page, pageSize, counts);
    }

    public async Task<AdminDestinationDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var d = await Load(id).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return d is null ? null : await ToDetailAsync(d, ct);
    }

    public async Task<AdminDestinationDetail> CreateAsync(CreateDestinationRequest request, string? actorId, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        Destination? parent = null;
        string country;
        if (request.ParentId is { } parentId)
        {
            parent = await db.Destinations.FirstOrDefaultAsync(d => d.Id == parentId, ct)
                ?? throw new AppException("That country doesn't exist.");
            if (parent.Kind != DestinationKind.Country)
                throw new AppException("A place can only be added inside a country.");
            country = parent.Country;
            if (!string.IsNullOrWhiteSpace(request.Country) && !request.Country.Trim().Equals(country, StringComparison.OrdinalIgnoreCase))
                throw new AppException($"{parent.Name} is {country}; a place inside it has the same country code.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Country))
                throw new AppException("Give the country code, such as BW.");
            country = request.Country.Trim().ToUpperInvariant();
        }

        string slug;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = request.Slug.Trim();
            if (await db.Destinations.AnyAsync(d => d.Slug == slug, ct))
                throw new ConflictException($"The address '{slug}' is already used by another destination.");
        }
        else
        {
            slug = await UniqueSlugAsync(name, ct);
        }

        var now = DateTime.UtcNow;
        var destination = new Destination
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = name,
            Country = country,
            Kind = parent is null ? DestinationKind.Country : DestinationKind.Place,
            Status = DestinationStatus.Draft,
            Parent = parent,
            Tagline = Clean(request.Tagline),
            Summary = Clean(request.Summary),
            Description = Clean(request.Description),
            BestTimeToVisit = Clean(request.BestTimeToVisit),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Destinations.Add(destination);
        Audit("DestinationCreated", destination.Id, actorId, new { destination.Slug, destination.Name, destination.Kind });
        await db.SaveChangesAsync(ct);

        return (await GetAsync(destination.Id, ct))!;
    }

    public async Task<AdminDestinationDetail?> UpdateAsync(Guid id, UpdateDestinationRequest request, string? actorId, CancellationToken ct = default)
    {
        var d = await LoadTracked(id, ct);
        if (d is null) return null;

        var changed = new List<string>();
        if (request.Name is not null && request.Name.Trim() != d.Name) { d.Name = request.Name.Trim(); changed.Add("name"); }
        if (request.Slug is not null && request.Slug.Trim() != d.Slug)
        {
            var slug = request.Slug.Trim();
            if (d.PublishedAt is not null)
                throw new ConflictException("This destination has been published, so its address can't change. Shared links would break.");
            if (await db.Destinations.AnyAsync(x => x.Slug == slug && x.Id != id, ct))
                throw new ConflictException($"The address '{slug}' is already used by another destination.");
            d.Slug = slug;
            changed.Add("slug");
        }
        Set(request.Tagline, v => d.Tagline = v, d.Tagline, "tagline");
        Set(request.Summary, v => d.Summary = v, d.Summary, "summary");
        Set(request.Description, v => d.Description = v, d.Description, "description");
        Set(request.BestTimeToVisit, v => d.BestTimeToVisit = v, d.BestTimeToVisit, "bestTimeToVisit");
        Set(request.HeroAlt, v => d.HeroAlt = v, d.HeroAlt, "heroAlt");
        Set(request.HeroCredit, v => d.HeroCredit = v, d.HeroCredit, "heroCredit");
        if (request.Latitude.HasValue && request.Latitude != d.Latitude) { d.Latitude = request.Latitude; changed.Add("latitude"); }
        if (request.Longitude.HasValue && request.Longitude != d.Longitude) { d.Longitude = request.Longitude; changed.Add("longitude"); }
        if (request.SortOrder.HasValue && request.SortOrder != d.SortOrder) { d.SortOrder = request.SortOrder.Value; changed.Add("sortOrder"); }

        // A published destination must keep what publishing required.
        if (d.Status == DestinationStatus.Published && string.IsNullOrWhiteSpace(d.Summary))
            throw new AppException("A published destination needs a summary. Unpublish it first to clear it.");

        if (changed.Count > 0)
        {
            d.UpdatedAt = DateTime.UtcNow;
            Audit("DestinationUpdated", d.Id, actorId, new { fields = changed });
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(id, ct);

        void Set(string? sent, Action<string?> apply, string? current, string field)
        {
            if (sent is null) return;
            var value = Clean(sent);
            if (value == current) return;
            apply(value);
            changed.Add(field);
        }
    }

    public async Task<AdminDestinationDetail?> ReplaceAttractionsAsync(
        Guid id, IReadOnlyList<AttractionInput> attractions, string? actorId, CancellationToken ct = default)
    {
        var d = await LoadTracked(id, ct);
        if (d is null) return null;

        if (attractions.Count > MaxAttractions) throw new AppException($"A destination can list at most {MaxAttractions} attractions.");
        if (d.Status == DestinationStatus.Published && d.Kind == DestinationKind.Place && attractions.Count == 0)
            throw new AppException("A published place needs at least one attraction. Unpublish it first to clear them.");

        var existing = d.Attractions.ToDictionary(a => a.Id);
        var seen = new HashSet<Guid>();
        foreach (var input in attractions)
        {
            if (input.Id is not { } keepId) continue;
            if (!existing.ContainsKey(keepId)) throw new AppException("One of the attractions doesn't belong to this destination.");
            if (!seen.Add(keepId)) throw new AppException("The same attraction is listed twice.");
        }

        var now = DateTime.UtcNow;
        var removedPhotos = d.Attractions.Where(a => !seen.Contains(a.Id) && a.PhotoPath is not null).Select(a => a.PhotoPath!).ToList();
        foreach (var gone in d.Attractions.Where(a => !seen.Contains(a.Id)).ToList())
            db.DestinationAttractions.Remove(gone);

        for (var i = 0; i < attractions.Count; i++)
        {
            var input = attractions[i];
            DestinationAttraction row;
            if (input.Id is { } keepId)
            {
                row = existing[keepId];
            }
            else
            {
                row = new DestinationAttraction { Id = Guid.NewGuid(), DestinationId = d.Id, CreatedAt = now };
                db.DestinationAttractions.Add(row);
            }
            row.Name = input.Name.Trim();
            row.Summary = Clean(input.Summary);
            row.PhotoAlt = Clean(input.PhotoAlt);
            row.PhotoCredit = Clean(input.PhotoCredit);
            row.SortOrder = i;
            row.UpdatedAt = now;
        }

        d.UpdatedAt = now;
        Audit("DestinationAttractionsReplaced", d.Id, actorId, new { count = attractions.Count });
        await db.SaveChangesAsync(ct);
        foreach (var path in removedPhotos) storage.Delete(path);
        return await GetAsync(id, ct);
    }

    public async Task<AdminDestinationDetail?> SetHeroAsync(
        Guid id, UploadedImage file, string? alt, string? credit, string? actorId, CancellationToken ct = default)
    {
        var d = await LoadTracked(id, ct);
        if (d is null) return null;

        var contentType = await CheckImageAsync(file, ct);
        var oldPath = d.HeroPath;
        await using var stream = file.Open();
        var stored = await storage.SaveAsync($"destinations/{d.Id:N}", stream, contentType, ct);
        try
        {
            d.HeroPath = stored.Path;
            d.HeroAlt = Clean(alt) ?? d.Name;
            d.HeroCredit = Clean(credit);   // the old credit belonged to the old photo
            d.UpdatedAt = DateTime.UtcNow;
            Audit("DestinationHeroChanged", d.Id, actorId, new { path = stored.Path });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(stored.Path);
            throw;
        }
        if (oldPath is not null) storage.Delete(oldPath);
        return await GetAsync(id, ct);
    }

    public async Task<AdminDestinationDetail?> SetAttractionPhotoAsync(
        Guid id, Guid attractionId, UploadedImage file, string? alt, string? credit, string? actorId, CancellationToken ct = default)
    {
        var d = await LoadTracked(id, ct);
        var attraction = d?.Attractions.FirstOrDefault(a => a.Id == attractionId);
        if (d is null || attraction is null) return null;

        var contentType = await CheckImageAsync(file, ct);
        var oldPath = attraction.PhotoPath;
        await using var stream = file.Open();
        var stored = await storage.SaveAsync($"destinations/{d.Id:N}", stream, contentType, ct);
        try
        {
            attraction.PhotoPath = stored.Path;
            attraction.PhotoAlt = Clean(alt) ?? attraction.Name;
            attraction.PhotoCredit = Clean(credit);
            attraction.UpdatedAt = DateTime.UtcNow;
            d.UpdatedAt = attraction.UpdatedAt;
            Audit("DestinationAttractionPhotoChanged", d.Id, actorId, new { attractionId, path = stored.Path });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(stored.Path);
            throw;
        }
        if (oldPath is not null) storage.Delete(oldPath);
        return await GetAsync(id, ct);
    }

    public async Task<DestinationPublishOutcome> PublishAsync(Guid id, string? actorId, CancellationToken ct = default)
    {
        var d = await LoadTracked(id, ct);
        if (d is null) return new DestinationPublishOutcome(null, []);

        var missing = Checklist(d);
        if (missing.Count > 0) return new DestinationPublishOutcome(await ToDetailAsync(d, ct), missing);

        var now = DateTime.UtcNow;
        d.Status = DestinationStatus.Published;
        d.PublishedAt ??= now;
        d.UpdatedAt = now;
        Audit("DestinationPublished", d.Id, actorId, new { d.Slug });
        await db.SaveChangesAsync(ct);
        return new DestinationPublishOutcome(await GetAsync(id, ct), []);
    }

    public async Task<AdminDestinationDetail?> UnpublishAsync(Guid id, string? actorId, CancellationToken ct = default)
    {
        var d = await LoadTracked(id, ct);
        if (d is null) return null;
        d.Status = DestinationStatus.Draft;
        d.UpdatedAt = DateTime.UtcNow;
        Audit("DestinationUnpublished", d.Id, actorId, new { d.Slug });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string? actorId, CancellationToken ct = default)
    {
        var d = await LoadTracked(id, ct);
        if (d is null) return false;

        if (d.Places.Count > 0)
            throw new ConflictException($"{d.Name} still has {d.Places.Count} place(s) inside it. Remove or move those first.");
        if (await db.Packages.AnyAsync(p => p.DestinationId == id, ct))
            throw new ConflictException($"Packages still use {d.Name}. Unpublish it instead of deleting it.");
        if (await db.Properties.AnyAsync(p => p.DestinationId == id, ct))
            throw new ConflictException($"Properties still use {d.Name}. Unpublish it instead of deleting it.");

        var files = d.Attractions.Select(a => a.PhotoPath).Append(d.HeroPath).Where(p => p is not null).Select(p => p!).ToList();
        db.Destinations.Remove(d);   // attractions go with it (cascade)
        Audit("DestinationDeleted", d.Id, actorId, new { d.Slug, d.Name });
        await db.SaveChangesAsync(ct);
        foreach (var path in files) storage.Delete(path);
        return true;
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────

    /// <summary>What a destination still needs before it can go live.</summary>
    internal static IReadOnlyList<string> Checklist(Destination d)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(d.HeroPath)) missing.Add("A hero photo");
        if (string.IsNullOrWhiteSpace(d.Summary)) missing.Add("A summary");
        if (d.Kind == DestinationKind.Place && d.Attractions.Count == 0) missing.Add("At least one attraction");
        if (d.Kind == DestinationKind.Country && !d.Places.Any(p => p.Status == DestinationStatus.Published))
            missing.Add("At least one published place");
        return missing;
    }

    private IQueryable<Destination> Load(Guid id) => db.Destinations
        .Include(d => d.Parent).Include(d => d.Places).Include(d => d.Attractions);

    private Task<Destination?> LoadTracked(Guid id, CancellationToken ct) => Load(id).FirstOrDefaultAsync(d => d.Id == id, ct);

    private async Task<AdminDestinationDetail> ToDetailAsync(Destination d, CancellationToken ct)
    {
        var ids = d.Places.Select(p => p.Id).Append(d.Id).ToList();
        var packageCount = await db.Packages.CountAsync(p => ids.Contains(p.DestinationId), ct);

        var places = new List<AdminDestinationSummary>();
        foreach (var p in d.Places.OrderBy(p => p.SortOrder).ThenBy(p => p.Name))
        {
            var attractionCount = await db.DestinationAttractions.CountAsync(a => a.DestinationId == p.Id, ct);
            places.Add(new AdminDestinationSummary(p.Id, p.Slug, p.Name, p.Country, p.Kind.ToString(), p.Status.ToString(),
                d.Name, p.HeroPath, 0, attractionCount, p.UpdatedAt));
        }

        return new AdminDestinationDetail(
            d.Id, d.Slug, d.Name, d.Country, d.Kind.ToString(), d.Status.ToString(),
            d.ParentId, d.Parent?.Name,
            d.Tagline, d.Summary, d.Description, d.BestTimeToVisit,
            d.HeroPath, d.HeroAlt, d.HeroCredit, d.Latitude, d.Longitude, d.SortOrder,
            d.PublishedAt is not null, d.PublishedAt, d.UpdatedAt,
            places,
            d.Attractions.OrderBy(a => a.SortOrder)
                .Select(a => new AttractionResponse(a.Id, a.Name, a.Summary, a.PhotoPath, a.PhotoAlt, a.PhotoCredit, a.SortOrder)).ToList(),
            packageCount,
            Checklist(d));
    }

    private async Task<string> CheckImageAsync(UploadedImage file, CancellationToken ct)
    {
        if (file.Length == 0) throw new AppException($"{file.FileName} is empty.");
        if (file.Length > _media.MaxFileSizeBytes)
            throw new AppException($"{file.FileName} is larger than {_media.MaxFileSizeBytes / (1024 * 1024)} MB.");

        var header = new byte[12];
        int read;
        await using (var stream = file.Open())
            read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        return storage.SniffContentType(header.AsSpan(0, read))
            ?? throw new AppException($"{file.FileName} isn't a JPEG, PNG or WebP photo.");
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = AdminPackageService.Slugify(name);
        if (baseSlug == "package") baseSlug = "destination";
        var slug = baseSlug;
        for (var n = 2; await db.Destinations.AnyAsync(d => d.Slug == slug, ct); n++)
            slug = $"{baseSlug}-{n}";
        return slug;
    }

    private void Audit(string action, Guid entityId, string? actorId, object after) => db.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid(),
        ActorId = actorId,
        Action = action,
        EntityType = nameof(Destination),
        EntityId = entityId.ToString(),
        After = JsonSerializer.Serialize(after),
        CreatedAt = DateTime.UtcNow,
    });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
