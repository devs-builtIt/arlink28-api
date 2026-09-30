using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Catalogue.RequestModels;
using Arlink28.Api.Features.Catalogue.Services;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Tests;

public sealed class CatalogueServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CatalogueService _service;
    private readonly Guid _destination = Guid.NewGuid();
    private readonly Guid _manor = Guid.NewGuid();
    private readonly Guid _camp = Guid.NewGuid();
    private readonly Guid _season = Guid.NewGuid();

    public CatalogueServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Destinations.Add(new Destination { Id = _destination, Slug = "nairobi", Name = "Nairobi", Country = "KE" });
        _db.Properties.Add(new Property { Id = _manor, Slug = "giraffe-manor", Name = "Giraffe Manor", DestinationId = _destination });
        _db.Properties.Add(new Property { Id = _camp, Slug = "salas-camp", Name = "Sala's Camp", DestinationId = _destination });
        _db.Seasons.Add(new Season
        {
            Id = _season, Slug = "peak", Name = "Peak",
            Ranges =
            [
                new SeasonRange { Id = Guid.NewGuid(), SeasonId = _season, StartDate = new DateOnly(2027, 6, 1), EndDate = new DateOnly(2027, 10, 31) },
                new SeasonRange { Id = Guid.NewGuid(), SeasonId = _season, StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 1, 5) },
            ],
        });
        _db.SaveChanges();
        _service = new CatalogueService(_db);
    }

    public void Dispose() => _db.Dispose();

    private Package Add(string title, long? from, int nights = 2, PackageStatus status = PackageStatus.Published,
        bool featured = false, string? summary = null)
    {
        var package = new Package
        {
            Id = Guid.NewGuid(), Slug = title.ToLowerInvariant().Replace(' ', '-'), Title = title, Summary = summary,
            Status = status, Category = "SAFARI", DestinationId = _destination, Nights = nights, MinNights = nights,
            Adults = 2, BaseCurrency = "USD", FromPriceMinor = from, Featured = featured,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.Packages.Add(package);
        _db.SaveChanges();
        return package;
    }

    private static PackageListRequest Paged(int page, int limit = 20, string? sort = null, string? q = null) =>
        new(null, null, null, null, null, null, limit, null, page, sort, q);

    [Fact]
    public async Task A_page_number_asks_for_numbered_pages_with_a_total()
    {
        for (var i = 1; i <= 7; i++) Add($"Package {i}", i * 100_00);
        Add("Hidden draft", 1_00, status: PackageStatus.Draft);

        var first = await _service.ListPackagesAsync(Paged(1, limit: 3));
        var last = await _service.ListPackagesAsync(Paged(3, limit: 3));

        Assert.Equal(7, first.Total); // drafts never count
        Assert.Equal((1, 3), (first.Page, first.PageSize));
        Assert.Equal(3, first.Items.Count);
        Assert.Single(last.Items);
        Assert.Null(first.NextCursor);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(last.Items.Select(i => i.Id)));
    }

    [Fact]
    public async Task Without_a_page_number_the_list_still_walks_by_cursor()
    {
        for (var i = 1; i <= 5; i++) Add($"Package {i}", i * 100_00);

        var first = await _service.ListPackagesAsync(new PackageListRequest(null, null, null, null, null, null, Limit: 2));

        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Null(first.Total);
    }

    [Fact]
    public async Task Sorting_puts_the_cheapest_first_and_packages_without_a_price_last()
    {
        Add("Mid", 500_00);
        Add("Cheap", 100_00);
        Add("Unpriced", null);
        Add("Dear", 900_00);

        var up = (await _service.ListPackagesAsync(Paged(1, sort: "price"))).Items.Select(i => i.Title);
        var down = (await _service.ListPackagesAsync(Paged(1, sort: "-price"))).Items.Select(i => i.Title);

        Assert.Equal(["Cheap", "Mid", "Dear", "Unpriced"], up);
        Assert.Equal(["Dear", "Mid", "Cheap", "Unpriced"], down);
    }

    [Fact]
    public async Task Sorting_by_nights_puts_the_shortest_first()
    {
        Add("Long", 100_00, nights: 7);
        Add("Short", 100_00, nights: 2);
        Add("Medium", 100_00, nights: 4);

        var titles = (await _service.ListPackagesAsync(Paged(1, sort: "nights"))).Items.Select(i => i.Title);

        Assert.Equal(["Short", "Medium", "Long"], titles);
    }

    [Fact]
    public async Task The_default_order_puts_featured_packages_first()
    {
        Add("Plain", 100_00);
        Add("Star", 100_00, featured: true);

        var titles = (await _service.ListPackagesAsync(Paged(1))).Items.Select(i => i.Title);

        Assert.Equal(["Star", "Plain"], titles);
    }

    [Fact]
    public async Task Search_matches_the_title_or_the_summary_in_any_case()
    {
        Add("Giraffe Manor Escape", 100_00, summary: "Breakfast with the giraffes");
        Add("Mara Safari", 100_00, summary: "Big cats on the plains");

        Assert.Equal("Mara Safari", (await _service.ListPackagesAsync(Paged(1, q: "MARA"))).Items.Single().Title);
        Assert.Equal("Giraffe Manor Escape", (await _service.ListPackagesAsync(Paged(1, q: "giraffes"))).Items.Single().Title);
        Assert.Empty((await _service.ListPackagesAsync(Paged(1, q: "zanzibar"))).Items);
    }

    [Fact]
    public async Task A_card_carries_up_to_three_highlights_with_highlights_first_and_the_lodges_in_order()
    {
        var package = Add("Escape", 100_00);
        var feature = new Feature { Id = Guid.NewGuid(), Slug = "meals", Label = "All meals", CreatedAt = DateTime.UtcNow };
        _db.Features.Add(feature);
        _db.PackageFeatures.AddRange(
            new PackageFeature { Id = Guid.NewGuid(), PackageId = package.Id, Section = FeatureSection.Included, FeatureId = feature.Id, SortOrder = 0 },
            new PackageFeature { Id = Guid.NewGuid(), PackageId = package.Id, Section = FeatureSection.Included, LabelOverride = "Laundry", SortOrder = 1 },
            new PackageFeature { Id = Guid.NewGuid(), PackageId = package.Id, Section = FeatureSection.Excluded, LabelOverride = "Tips", SortOrder = 2 },
            new PackageFeature { Id = Guid.NewGuid(), PackageId = package.Id, Section = FeatureSection.Highlight, LabelOverride = "Feed the giraffes", SortOrder = 0 },
            new PackageFeature { Id = Guid.NewGuid(), PackageId = package.Id, Section = FeatureSection.Included, LabelOverride = "Wi-Fi", SortOrder = 3 });
        _db.PackageStays.AddRange(
            new PackageStay { Id = Guid.NewGuid(), PackageId = package.Id, PropertyId = _camp, Nights = 1, SortOrder = 1 },
            new PackageStay { Id = Guid.NewGuid(), PackageId = package.Id, PropertyId = _manor, Nights = 1, SortOrder = 0 },
            new PackageStay { Id = Guid.NewGuid(), PackageId = package.Id, PropertyId = _manor, Nights = 1, SortOrder = 2 });
        await _db.SaveChangesAsync();

        var card = (await _service.ListPackagesAsync(Paged(1))).Items.Single();

        Assert.Equal(["Feed the giraffes", "All meals", "Laundry"], card.Highlights); // never the exclusions
        Assert.Equal(["Giraffe Manor", "Sala's Camp"], card.Lodges);
    }

    [Fact]
    public async Task The_detail_gives_each_rate_the_dates_it_covers_in_order()
    {
        var package = Add("Escape", 100_00);
        _db.PackageRates.Add(new PackageRate
        {
            Id = Guid.NewGuid(), PackageId = package.Id, SeasonId = _season, Currency = "USD", PriceMinor = 100_00,
        });
        await _db.SaveChangesAsync();

        var detail = await _service.GetPackageAsync(package.Slug);

        var ranges = Assert.Single(detail!.Rates).Ranges;
        Assert.Equal([new DateOnly(2027, 1, 1), new DateOnly(2027, 6, 1)], ranges.Select(r => r.Start));
    }
}
