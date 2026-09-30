using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminPackages.RequestModels;
using Arlink28.Api.Features.AdminPackages.ResponseModels;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Features.Catalogue.ResponseModels;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers;
using Arlink28.Api.Helpers.Settings;
using static Arlink28.Api.Features.AdminPackages.Services.AdminPackageMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Features.AdminPackages.Services;

public class AdminPackageService(
    ApplicationDbContext db,
    IMediaStorage storage,
    IOptions<MediaStorageSettings> mediaOptions) : IAdminPackageService, IScoped
{
    private readonly MediaStorageSettings _media = mediaOptions.Value;

    public async Task<AdminPackageListResponse> ListAsync(
        string? status, string? search, string? destination, string? category, int page, int pageSize, string? type = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var matching = db.Packages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            matching = matching.Where(p => p.Title.ToLower().Contains(term) || p.Slug.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(destination))
            matching = matching.Where(p => p.Destination.Slug == destination);
        if (Enum.TryParse<ProductType>(type, ignoreCase: true, out var productType))
            matching = matching.Where(p => p.ProductType == productType);
        if (!string.IsNullOrWhiteSpace(category))
            matching = matching.Where(p => p.Category == category.Trim().ToUpper());

        // The tabs show every status for the search and filters, whichever tab is open.
        var byStatus = await matching.GroupBy(p => p.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int CountOf(PackageStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        var counts = new StatusCounts(byStatus.Sum(x => x.Count), CountOf(PackageStatus.Draft),
            CountOf(PackageStatus.Published), CountOf(PackageStatus.Archived));

        var filtered = Enum.TryParse<PackageStatus>(status, ignoreCase: true, out var parsed)
            ? matching.Where(p => p.Status == parsed)
            : matching;
        var total = await filtered.CountAsync(ct);

        var packages = await filtered
            .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Title).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(p => p.Destination).Include(p => p.Media)
            .ToListAsync(ct);

        var items = packages.Select(p => new AdminPackageSummary(
            p.Id, p.Slug, p.Title, p.Status.ToString(), p.Category, ToResponse(p.Destination),
            p.Nights, p.Adults, p.Children, p.BaseCurrency, p.FromPriceMinor,
            p.Media.Where(m => m.Role == MediaRole.Hero).OrderBy(m => m.SortKey).Select(m => m.Path).FirstOrDefault(),
            p.Media.Count, p.UpdatedAt, p.ProductType.ToString(), ProductDetails.ToJson(p.Details))).ToList();
        return new AdminPackageListResponse(items, total, page, pageSize, counts);
    }

    public async Task<AdminPackageDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var package = await db.Packages.AsNoTracking().WithContent().FirstOrDefaultAsync(p => p.Id == id, ct);
        return package is null ? null : ToDetail(package);
    }

    public async Task<AdminPackageDetail> CreateAsync(CreatePackageRequest request, CancellationToken ct = default)
    {
        var destination = await db.Destinations.FirstOrDefaultAsync(d => d.Id == request.DestinationId, ct)
            ?? throw new AppException("That destination doesn't exist.");

        var productType = request.ProductType ?? ProductType.HolidayPackage;
        var details = ProductDetails.Normalize(productType, request.Details);

        var now = DateTime.UtcNow;
        var package = new Package
        {
            ProductType = productType,
            Details = details,
            FromPriceMinor = productType == ProductType.HolidayPackage || request.FromPriceMinor is null or 0
                ? null : request.FromPriceMinor,
            Slug = await UniqueSlugAsync(request.Title, ct),
            Status = PackageStatus.Draft,
            Title = request.Title.Trim(),
            Subtitle = Clean(request.Subtitle),
            Summary = Clean(request.Summary),
            Description = Clean(request.Description),
            // Only holidays have a style (Safari, Lodge...); the other types are labelled by their own name.
            Category = productType == ProductType.HolidayPackage ? request.Category.Trim().ToUpperInvariant() : productType.ToString().ToUpperInvariant(),
            Destination = destination,
            Nights = request.Nights,
            MinNights = request.Nights,
            Adults = request.Adults,
            Children = request.Children,
            PricingBasis = request.PricingBasis ?? PricingBasis.PerParty,
            BaseCurrency = request.BaseCurrency?.Trim().ToUpperInvariant() ?? "USD",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Packages.Add(package);
        await db.SaveChangesAsync(ct);
        return ToDetail(package);
    }

    public async Task<AdminPackageDetail?> UpdateAsync(Guid id, UpdatePackageRequest request, CancellationToken ct = default)
    {
        var package = await db.Packages.WithContent().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (package is null) return null;

        if (request.DestinationId is { } destinationId && destinationId != package.DestinationId)
        {
            package.Destination = await db.Destinations.FirstOrDefaultAsync(d => d.Id == destinationId, ct)
                ?? throw new AppException("That destination doesn't exist.");
        }

        if (request.Details is not null)
        {
            if (package.ProductType == ProductType.HolidayPackage)
                throw new AppException("Holiday packages don't have a details object; edit their stays and rates instead.");
            package.Details = ProductDetails.Normalize(package.ProductType, request.Details);
        }

        // The slug stays put on a rename: it's the customer-facing URL.
        if (request.Title is not null) package.Title = request.Title.Trim();
        if (request.Subtitle is not null) package.Subtitle = Clean(request.Subtitle);
        if (request.Summary is not null) package.Summary = Clean(request.Summary);
        if (request.Description is not null) package.Description = Clean(request.Description);
        if (request.Category is not null) package.Category = request.Category.Trim().ToUpperInvariant();
        if (request.Adults is not null) package.Adults = request.Adults.Value;
        if (request.Children is not null) package.Children = request.Children.Value;
        if (request.PricingBasis is not null) package.PricingBasis = request.PricingBasis.Value;
        if (request.BaseCurrency is not null) package.BaseCurrency = request.BaseCurrency.Trim().ToUpperInvariant();
        if (request.Featured is not null) package.Featured = request.Featured.Value;
        if (request.SeoTitle is not null) package.SeoTitle = Clean(request.SeoTitle);
        if (request.SeoDescription is not null) package.SeoDescription = Clean(request.SeoDescription);
        if (request.Nights is not null)
        {
            package.Nights = request.Nights.Value;
            package.MinNights = Math.Min(package.MinNights, package.Nights);
        }
        if (request.MinNights is not null) package.MinNights = request.MinNights.Value;
        if (package.ProductType == ProductType.HolidayPackage && package.Nights < 1)
            throw new AppException("A holiday package needs at least one night.");
        if (package.MinNights > package.Nights)
            throw new AppException("The minimum stay can't be longer than the package's nights.");

        if (package.ProductType == ProductType.HolidayPackage)
        {
            if (request.FromPriceMinor is not null)
                throw new AppException("A holiday package's from-price comes from its season rates.");
            // The from-price only counts base-currency rates, so a currency change moves it.
            package.FromPriceMinor = AdminPackageMapper.FromPrice(package, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        else if (request.FromPriceMinor is { } price)
        {
            package.FromPriceMinor = price == 0 ? null : price;
        }

        package.UpdatedAt = DateTime.UtcNow;
        package.Version++;
        await db.SaveChangesAsync(ct);
        return ToDetail(package);
    }

    public async Task<IReadOnlyList<MediaResponse>?> AddPhotosAsync(
        Guid id, IReadOnlyList<UploadedImage> files, CancellationToken ct = default)
    {
        var package = await db.Packages.Include(p => p.Media).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (package is null) return null;

        if (files.Count == 0) throw new AppException("Choose at least one photo.");
        if (files.Count > _media.MaxFilesPerRequest)
            throw new AppException($"Upload at most {_media.MaxFilesPerRequest} photos at a time.");

        // Check every file before writing any, so a bad one doesn't leave the rest half-uploaded.
        var checkedFiles = new List<(UploadedImage File, string ContentType)>();
        foreach (var file in files)
        {
            if (file.Length == 0) throw new AppException($"{file.FileName} is empty.");
            if (file.Length > _media.MaxFileSizeBytes)
                throw new AppException($"{file.FileName} is larger than {_media.MaxFileSizeBytes / (1024 * 1024)} MB.");

            var header = new byte[12];
            int read;
            await using (var stream = file.Open())
                read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

            var contentType = storage.SniffContentType(header.AsSpan(0, read))
                ?? throw new AppException($"{file.FileName} isn't a JPEG, PNG or WebP photo.");
            checkedFiles.Add((file, contentType));
        }

        var saved = new List<PackageMedia>();
        var nextSort = package.Media.Count == 0 ? 0 : package.Media.Max(m => m.SortKey) + 1;
        var hasHero = package.Media.Any(m => m.Role == MediaRole.Hero);
        try
        {
            foreach (var (file, contentType) in checkedFiles)
            {
                await using var stream = file.Open();
                var stored = await storage.SaveAsync($"packages/{package.Id:N}", stream, contentType, ct);

                var item = new PackageMedia
                {
                    PackageId = package.Id,
                    Role = hasHero ? MediaRole.Gallery : MediaRole.Hero,
                    Path = stored.Path,
                    Alt = AltFromFileName(file.FileName, package.Title),
                    SortKey = nextSort++,
                    CreatedAt = DateTime.UtcNow,
                };
                hasHero = true; // the first photo of an empty package is its Hero
                saved.Add(item);
                db.PackageMedia.Add(item);
            }

            package.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var item in saved) storage.Delete(item.Path);
            throw;
        }

        return saved.Select(ToResponse).ToList();
    }

    public async Task<MediaResponse?> UpdateMediaAsync(
        Guid id, Guid mediaId, UpdateMediaRequest request, CancellationToken ct = default)
    {
        var all = await db.PackageMedia.Where(m => m.PackageId == id).ToListAsync(ct);
        var item = all.FirstOrDefault(m => m.Id == mediaId);
        if (item is null) return null;

        if (request.Alt is not null) item.Alt = Clean(request.Alt);
        if (request.Caption is not null) item.Caption = Clean(request.Caption);

        if (request.Role is { } role && role != item.Role)
        {
            if (item.VideoProvider is not null && role != MediaRole.Gallery)
                throw new AppException("A video can only sit in the gallery.");

            if (role == MediaRole.Hero)
                foreach (var other in all.Where(m => m.Role == MediaRole.Hero && m.Id != item.Id))
                    other.Role = MediaRole.Gallery;
            item.Role = role;
        }

        await TouchAsync(id, ct);
        await db.SaveChangesAsync(ct);
        return ToResponse(item);
    }

    public async Task<IReadOnlyList<MediaResponse>?> ReorderMediaAsync(
        Guid id, ReorderMediaRequest request, CancellationToken ct = default)
    {
        var all = await db.PackageMedia.Where(m => m.PackageId == id).ToListAsync(ct);
        if (all.Count == 0 && !await db.Packages.AnyAsync(p => p.Id == id, ct)) return null;

        var ids = request.Ids.ToHashSet();
        if (ids.Count != request.Ids.Count || !ids.SetEquals(all.Select(m => m.Id)))
            throw new AppException("Send every photo of this package exactly once.");

        for (var i = 0; i < request.Ids.Count; i++)
            all.First(m => m.Id == request.Ids[i]).SortKey = i;

        await TouchAsync(id, ct);
        await db.SaveChangesAsync(ct);
        return all.OrderBy(m => m.SortKey).Select(ToResponse).ToList();
    }

    public async Task<bool> DeleteMediaAsync(Guid id, Guid mediaId, CancellationToken ct = default)
    {
        var all = await db.PackageMedia.Where(m => m.PackageId == id).ToListAsync(ct);
        var item = all.FirstOrDefault(m => m.Id == mediaId);
        if (item is null) return false;

        db.PackageMedia.Remove(item);

        // A package with photos always keeps a Hero: the next photo takes over.
        if (item.Role == MediaRole.Hero)
        {
            var next = all.Where(m => m.Id != item.Id && m.VideoProvider is null && m.Role == MediaRole.Gallery)
                .OrderBy(m => m.SortKey).FirstOrDefault();
            if (next is not null) next.Role = MediaRole.Hero;
        }

        await TouchAsync(id, ct);
        await db.SaveChangesAsync(ct);
        storage.Delete(item.Path); // after the row is gone, so a failed save never orphans a row
        return true;
    }

    private async Task TouchAsync(Guid packageId, CancellationToken ct)
    {
        var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == packageId, ct);
        if (package is not null) package.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> UniqueSlugAsync(string title, CancellationToken ct)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        for (var n = 2; await db.Packages.AnyAsync(p => p.Slug == slug, ct); n++)
            slug = $"{baseSlug}-{n}";
        return slug;
    }

    public static string Slugify(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var plain = new string(decomposed
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(plain, "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 90) slug = slug[..90].Trim('-');
        return slug.Length == 0 ? "package" : slug;
    }

    /// <summary>"giraffe-manor_lawn (2).JPG" becomes "giraffe manor lawn", or the title when nothing useful is left.</summary>
    private static string AltFromFileName(string fileName, string fallback)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        name = Regex.Replace(name, @"\(\d+\)|[-_.]+", " ");
        name = Regex.Replace(name, @"\s+", " ").Trim();
        // Camera names like IMG 4812 or DSC0031 say nothing about the photo.
        if (name.Length < 3 || Regex.IsMatch(name, @"^(img|dsc|dscn|pxl|photo|image|screenshot)\s*\d*$", RegexOptions.IgnoreCase))
            return fallback;
        return name.Length > 250 ? name[..250] : name;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
