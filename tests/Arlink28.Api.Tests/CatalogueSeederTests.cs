using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arlink28.Api.Tests;

public sealed class CatalogueSeederTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    /// <summary>The six flyers in the "Giraffe Manor" folder (2026-10-08), with the prices printed on them in US dollars.</summary>
    private static readonly Dictionary<string, long> FlyerPrices = new()
    {
        ["giraffe-manor-solo-escape"] = 698_670,
        ["giraffe-manor-signature-escape"] = 1_413_000,
        ["giraffe-manor-family-experience"] = 2_657_600,
        ["giraffe-manor-extended-family-package"] = 2_863_350,
        ["giraffe-manor-family-and-friends-retreat-3-adults"] = 3_720_640,
        ["giraffe-manor-family-and-friends-retreat-4-adults"] = 4_154_220,
    };

    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private readonly CatalogueSeeder _seeder;

    public CatalogueSeederTests() => _seeder = new CatalogueSeeder(_db, NullLogger<CatalogueSeeder>.Instance);

    public void Dispose() => _db.Dispose();

    private Task<IReadOnlyList<SeedResult>> SeedGiraffeManor() =>
        _seeder.SeedAsync(Today, FlyerPrices.Keys.ToHashSet());

    [Fact]
    public void The_whole_seed_is_valid()
    {
        CatalogueSeeder.Validate();
    }

    [Fact]
    public async Task Seeding_only_the_flyer_packages_loads_just_those_and_what_they_need()
    {
        var results = await SeedGiraffeManor();

        Assert.Equal(FlyerPrices.Keys.Order(), results.Select(r => r.Slug).Order());
        Assert.All(results, r => Assert.Equal("created", r.Action));
        Assert.Equal(FlyerPrices.Keys.Order(), (await _db.Packages.Select(p => p.Slug).ToListAsync()).Order());

        // Nairobi and its country, the one property, its partner and its season: nothing else.
        Assert.Equal(["kenya", "nairobi"], (await _db.Destinations.Select(d => d.Slug).ToListAsync()).Order());
        Assert.Equal(["giraffe-manor"], await _db.Properties.Select(p => p.Slug).ToListAsync());
        Assert.Equal(["giraffe-manor"], await _db.Partners.Select(p => p.Slug).ToListAsync());
        Assert.Equal(["giraffe-manor-2026"], await _db.Seasons.Select(s => s.Slug).ToListAsync());
        Assert.DoesNotContain(await _db.Features.Select(f => f.Slug).ToListAsync(), slug => slug.Contains("plunge"));
    }

    [Fact]
    public async Task The_flyer_packages_are_published_featured_and_priced_as_printed()
    {
        await SeedGiraffeManor();
        var packages = await _db.Packages.Include(p => p.Rates).Include(p => p.Media).ToListAsync();

        Assert.All(packages, p =>
        {
            Assert.Equal(PackageStatus.Published, p.Status);
            Assert.NotNull(p.PublishedAt);
            Assert.True(p.Featured);
            Assert.Equal(p.Nights, p.MinNights);
            Assert.Equal(PricingBasis.PerParty, p.PricingBasis);
            Assert.Single(p.Rates);
            Assert.Equal(FlyerPrices[p.Slug], p.Rates.Single().PriceMinor);
            Assert.Equal(FlyerPrices[p.Slug], p.FromPriceMinor);
            Assert.Single(p.Media, m => m.Role == MediaRole.Hero);
        });
    }

    [Theory]
    [InlineData("giraffe-manor-solo-escape", 3, 1, 0)]
    [InlineData("giraffe-manor-signature-escape", 5, 2, 0)]
    [InlineData("giraffe-manor-family-experience", 5, 2, 2)]
    [InlineData("giraffe-manor-extended-family-package", 5, 2, 3)]
    [InlineData("giraffe-manor-family-and-friends-retreat-3-adults", 7, 3, 0)]
    [InlineData("giraffe-manor-family-and-friends-retreat-4-adults", 7, 4, 0)]
    public async Task Each_package_has_the_party_and_nights_on_its_flyer(string slug, int nights, int adults, int children)
    {
        await SeedGiraffeManor();
        var p = await _db.Packages.Include(x => x.Stays).SingleAsync(x => x.Slug == slug);

        Assert.Equal(nights, p.Nights);
        Assert.Equal(adults, p.Adults);
        Assert.Equal(children, p.Children);
        Assert.Equal(nights, p.Stays.Sum(s => s.Nights));
        Assert.False(string.IsNullOrWhiteSpace(p.Stays.Single().RoomType));
    }

    [Fact]
    public async Task The_notes_sheet_is_on_every_package_and_the_third_child_rule_only_on_the_extended_family()
    {
        await SeedGiraffeManor();
        var features = await _db.PackageFeatures.Include(f => f.Package).ToListAsync();

        foreach (var slug in FlyerPrices.Keys)
        {
            var notes = features.Where(f => f.Package.Slug == slug && f.Section == FeatureSection.Note).Select(f => f.LabelOverride).ToList();
            Assert.Contains(notes, n => n!.StartsWith("Check-in is from 12 noon"));
            Assert.Contains(notes, n => n!.StartsWith("Children aged 3 to 11"));
            Assert.Equal(slug == "giraffe-manor-extended-family-package", notes.Any(n => n!.Contains("third child")));
        }
        // Inclusions and exclusions come from the shared library, in the order the flyers print them.
        var signature = features.Where(f => f.Package.Slug == "giraffe-manor-signature-escape");
        Assert.Equal(10, signature.Count(f => f.Section == FeatureSection.Included));
        Assert.Equal(3, signature.Count(f => f.Section == FeatureSection.PremiumService));
        Assert.Equal(7, signature.Count(f => f.Section == FeatureSection.Excluded));
    }

    [Fact]
    public async Task The_optional_park_drive_is_an_add_on_at_490_dollars()
    {
        await SeedGiraffeManor();
        var addOns = await _db.PackageAddOns.ToListAsync();

        Assert.Equal(FlyerPrices.Count, addOns.Count);
        Assert.All(addOns, a =>
        {
            Assert.Equal("Private Nairobi National Park vehicle", a.Name);
            Assert.Equal(49_000, a.PriceMinor);
            Assert.Equal("USD", a.Currency);
        });
    }

    [Fact]
    public async Task Seeding_again_updates_in_place_without_duplicating_anything()
    {
        await SeedGiraffeManor();
        var again = await SeedGiraffeManor();

        Assert.All(again, r => Assert.Equal("updated", r.Action));
        Assert.Equal(FlyerPrices.Count, await _db.Packages.CountAsync());
        Assert.Equal(FlyerPrices.Count, await _db.PackageRates.CountAsync());
        Assert.Equal(FlyerPrices.Count, await _db.PackageAddOns.CountAsync());
        Assert.Equal(FlyerPrices.Count, await _db.PackageMedia.CountAsync());
        Assert.Equal(1, await _db.Properties.CountAsync());
    }

    [Fact]
    public async Task An_unknown_slug_stops_the_seed_before_anything_is_written()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _seeder.SeedAsync(Today, new HashSet<string> { "giraffe-manor-solo-escape", "no-such-package" }));

        Assert.Contains("no-such-package", ex.Message);
        Assert.Empty(await _db.Packages.ToListAsync());
        Assert.Empty(await _db.Destinations.ToListAsync());
    }
}
