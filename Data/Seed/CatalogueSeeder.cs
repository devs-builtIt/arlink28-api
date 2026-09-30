using System.Text.Json;
using Arlink28.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Data.Seed;

public record SeedResult(string Slug, PackageStatus Status, string Action, string? DataIssue);

/// <summary>
/// Loads the poster catalogue (CatalogueSeedData). Idempotent: rows are matched by
/// slug, and each seeded package's stays, features, rates and add-ons are replaced,
/// so re-running resets those packages to the poster data. Packages not in the seed
/// are untouched, and a hero image is only added to a package that has none.
///
/// Run with <c>dotnet run -- seed-catalogue</c> (see Program.cs), not at startup:
/// once staff edit packages in the admin, a restart must not overwrite them.
/// </summary>
public class CatalogueSeeder(ApplicationDbContext db, ILogger<CatalogueSeeder> logger)
{
    /// <summary>
    /// Checks the seed against the catalogue invariants (docs/packages-api-plan.md §3)
    /// before touching the database, so a bad transcription fails loudly instead of
    /// publishing a wrong price.
    /// </summary>
    public static void Validate()
    {
        var problems = new List<string>();
        var features = CatalogueSeedData.Features.Select(f => f.Slug).ToHashSet();
        var seasons = CatalogueSeedData.Seasons.ToDictionary(s => s.Slug);
        var properties = CatalogueSeedData.Properties.Select(p => p.Slug).ToHashSet();
        var destinations = CatalogueSeedData.Destinations.Select(d => d.Slug).ToHashSet();
        var slugs = new HashSet<string>();

        foreach (var p in CatalogueSeedData.Packages)
        {
            var where = $"package {p.Slug}";
            if (!slugs.Add(p.Slug)) problems.Add($"{where}: duplicate slug");
            if (!destinations.Contains(p.Destination)) problems.Add($"{where}: unknown destination {p.Destination}");
            if (p.MinNights < 1 || p.Nights < p.MinNights) problems.Add($"{where}: nights/minNights out of order");
            var stayNights = p.Stays.Sum(s => s.Nights);
            if (p.Stays.Length > 0 && stayNights != p.Nights)
                problems.Add($"{where}: stays sum to {stayNights}, not {p.Nights}");
            foreach (var s in p.Stays.Where(s => !properties.Contains(s.Property)))
                problems.Add($"{where}: unknown property {s.Property}");
            foreach (var f in p.Features.Values.SelectMany(x => x).Where(f => f.Key is not null && !features.Contains(f.Key)))
                problems.Add($"{where}: unknown feature {f.Key}");
            if (p.Rates.Length == 0) problems.Add($"{where}: no rates");
            if (p.Status == PackageStatus.Draft && string.IsNullOrWhiteSpace(p.DataIssue))
                problems.Add($"{where}: Draft without a DataIssue");

            // Invariant 3: one base-currency price per check-in date.
            var ranges = new List<(string Season, string Start, string End)>();
            foreach (var r in p.Rates)
            {
                if (!seasons.TryGetValue(r.Season, out var season)) problems.Add($"{where}: unknown season {r.Season}");
                else ranges.AddRange(season.Ranges.Select(x => (r.Season, x.Start, x.End)));
            }
            foreach (var a in ranges)
            foreach (var b in ranges)
            {
                if (a.Season != b.Season && string.CompareOrdinal(a.Start, b.End) <= 0 && string.CompareOrdinal(b.Start, a.End) <= 0)
                    problems.Add($"{where}: seasons {a.Season} and {b.Season} overlap");
            }
        }

        if (problems.Count > 0)
            throw new InvalidOperationException("Invalid catalogue seed:\n- " + string.Join("\n- ", problems.Distinct()));
    }

    /// <summary>
    /// The lowest base-currency rate among seasons that still have a check-in date on
    /// or after <paramref name="today"/>; null when every season has ended.
    /// </summary>
    public static long? FromPrice(SeedPackage p, DateOnly today) =>
        p.Rates
            .Where(r => CatalogueSeedData.Seasons.First(s => s.Slug == r.Season).Ranges.Any(x => DateOnly.Parse(x.End) >= today))
            .Select(r => (long?)r.PriceMinor)
            .Min();

    public async Task<IReadOnlyList<SeedResult>> SeedAsync(DateOnly today, CancellationToken ct = default)
    {
        Validate();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var now = DateTime.UtcNow;

            var destinations = new Dictionary<string, Destination>();
            foreach (var d in CatalogueSeedData.Destinations)
            {
                var row = await db.Destinations.FirstOrDefaultAsync(x => x.Slug == d.Slug, ct)
                          ?? db.Destinations.Add(new Destination { Id = Guid.NewGuid(), Slug = d.Slug, CreatedAt = now }).Entity;
                row.Name = d.Name;
                row.Country = d.Country;
                row.UpdatedAt = now;
                destinations[d.Slug] = row;
            }

            var partners = new Dictionary<string, Partner>();
            foreach (var p in CatalogueSeedData.Partners)
            {
                var row = await db.Partners.FirstOrDefaultAsync(x => x.Slug == p.Slug, ct)
                          ?? db.Partners.Add(new Partner { Id = Guid.NewGuid(), Slug = p.Slug, CreatedAt = now }).Entity;
                row.Name = p.Name;
                row.Tagline = p.Tagline;
                row.UpdatedAt = now;
                partners[p.Slug] = row;
            }

            var properties = new Dictionary<string, Property>();
            foreach (var p in CatalogueSeedData.Properties)
            {
                var row = await db.Properties.FirstOrDefaultAsync(x => x.Slug == p.Slug, ct)
                          ?? db.Properties.Add(new Property { Id = Guid.NewGuid(), Slug = p.Slug, CreatedAt = now }).Entity;
                row.Name = p.Name;
                row.PartnerId = partners[p.Partner].Id;
                row.DestinationId = destinations[p.Destination].Id;
                row.UpdatedAt = now;
                properties[p.Slug] = row;
            }

            var features = new Dictionary<string, Feature>();
            foreach (var f in CatalogueSeedData.Features)
            {
                var row = await db.Features.FirstOrDefaultAsync(x => x.Slug == f.Slug, ct)
                          ?? db.Features.Add(new Feature { Id = Guid.NewGuid(), Slug = f.Slug, CreatedAt = now }).Entity;
                row.Label = f.Label;
                row.Icon = f.Icon;
                features[f.Slug] = row;
            }

            var seasons = new Dictionary<string, Season>();
            foreach (var s in CatalogueSeedData.Seasons)
            {
                var row = await db.Seasons.Include(x => x.Ranges).FirstOrDefaultAsync(x => x.Slug == s.Slug, ct)
                          ?? db.Seasons.Add(new Season { Id = Guid.NewGuid(), Slug = s.Slug, CreatedAt = now }).Entity;
                row.Name = s.Name;
                row.PartnerId = partners[s.Partner].Id;
                row.UpdatedAt = now;
                db.SeasonRanges.RemoveRange(row.Ranges);
                foreach (var (start, end) in s.Ranges)
                {
                    db.SeasonRanges.Add(new SeasonRange
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = row.Id,
                        StartDate = DateOnly.Parse(start),
                        EndDate = DateOnly.Parse(end),
                    });
                }
                seasons[s.Slug] = row;
            }

            // Save reference data first, so replaced season ranges are gone before packages are written.
            await db.SaveChangesAsync(ct);

            var results = new List<SeedResult>();
            foreach (var p in CatalogueSeedData.Packages)
            {
                var existing = await db.Packages
                    .Include(x => x.Stays).Include(x => x.Features).Include(x => x.Rates)
                    .Include(x => x.AddOns).Include(x => x.Media)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Slug == p.Slug, ct);
                var package = existing
                              ?? db.Packages.Add(new Package { Id = Guid.NewGuid(), Slug = p.Slug, CreatedAt = now }).Entity;

                package.Status = p.Status;
                package.Title = p.Title;
                package.Subtitle = p.Subtitle;
                package.Summary = p.Summary;
                package.Category = p.Category;
                package.DestinationId = destinations[p.Destination].Id;
                package.Nights = p.Nights;
                package.MinNights = p.MinNights;
                package.Adults = p.Adults;
                package.Children = p.Children;
                package.PricingBasis = PricingBasis.PerParty;
                package.BaseCurrency = CatalogueSeedData.BaseCurrency;
                package.FromPriceMinor = FromPrice(p, today);
                package.SortOrder = p.SortOrder;
                package.PublishedAt = p.Status == PackageStatus.Published ? existing?.PublishedAt ?? now : null;
                package.UpdatedAt = now;
                if (existing is not null) package.Version++;

                // Child lists are replaced wholesale, the same way the admin API's PUTs will.
                db.PackageStays.RemoveRange(package.Stays);
                db.PackageFeatures.RemoveRange(package.Features);
                db.PackageRates.RemoveRange(package.Rates);
                db.PackageAddOns.RemoveRange(package.AddOns);

                for (var i = 0; i < p.Stays.Length; i++)
                {
                    var s = p.Stays[i];
                    db.PackageStays.Add(new PackageStay
                    {
                        Id = Guid.NewGuid(), PackageId = package.Id, PropertyId = properties[s.Property].Id,
                        Nights = s.Nights, RoomType = s.RoomType, SortOrder = i,
                    });
                }

                foreach (var (section, items) in p.Features)
                {
                    for (var i = 0; i < items.Length; i++)
                    {
                        var f = items[i];
                        db.PackageFeatures.Add(new PackageFeature
                        {
                            Id = Guid.NewGuid(), PackageId = package.Id, Section = section,
                            FeatureId = f.Key is null ? null : features[f.Key].Id,
                            LabelOverride = f.Key is null ? f.Text : f.Label,
                            Footnote = f.Footnote, SortOrder = i,
                        });
                    }
                }

                foreach (var r in p.Rates)
                {
                    db.PackageRates.Add(new PackageRate
                    {
                        Id = Guid.NewGuid(), PackageId = package.Id, SeasonId = seasons[r.Season].Id,
                        Currency = CatalogueSeedData.BaseCurrency, PriceMinor = r.PriceMinor,
                    });
                }

                for (var i = 0; i < p.AddOns.Length; i++)
                {
                    var a = p.AddOns[i];
                    db.PackageAddOns.Add(new PackageAddOn
                    {
                        Id = Guid.NewGuid(), PackageId = package.Id, Name = a.Name, Description = a.Description,
                        Unit = a.Unit, Currency = CatalogueSeedData.BaseCurrency, PriceMinor = a.PriceMinor, SortOrder = i,
                    });
                }

                if (p.HeroImage is not null && !package.Media.Any(m => m.Role == MediaRole.Hero))
                {
                    db.PackageMedia.Add(new PackageMedia
                    {
                        Id = Guid.NewGuid(), PackageId = package.Id, Role = MediaRole.Hero, Path = p.HeroImage,
                        Alt = p.Title, VariantsReady = true, SortKey = 0, CreatedAt = now,
                    });
                }

                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    ActorId = null,
                    Action = existing is null ? "seed.package.create" : "seed.package.update",
                    EntityType = "package",
                    EntityId = package.Id.ToString(),
                    After = JsonSerializer.Serialize(new { p.Slug, Status = p.Status.ToString(), p.Rates, p.Sources }),
                    CreatedAt = now,
                });

                results.Add(new SeedResult(p.Slug, p.Status, existing is null ? "created" : "updated", p.DataIssue));
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            logger.LogInformation("Catalogue seeded: {Count} packages", results.Count);
            return (IReadOnlyList<SeedResult>)results;
        });
    }
}
