using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminPackages.RequestModels;
using Arlink28.Api.Features.AdminPackages.Services;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Features.Shared.Services;
using Arlink28.Api.Helpers;
using Arlink28.Api.Helpers.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Arlink28.Api.Tests;

public sealed class AdminPackageContentServiceTests : IDisposable
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "arlink28-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationDbContext _db;
    private readonly AdminPackageService _packages;
    private readonly AdminPackageContentService _content;

    private readonly Guid _destination = Guid.NewGuid();
    private readonly Guid _manor = Guid.NewGuid();
    private readonly Guid _camp = Guid.NewGuid();
    private readonly Guid _peak = Guid.NewGuid();
    private readonly Guid _savings = Guid.NewGuid();
    private readonly Guid _overlapsPeak = Guid.NewGuid();
    private readonly Guid _ended = Guid.NewGuid();
    private readonly Guid _champagne = Guid.NewGuid();

    public AdminPackageContentServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        _db.Destinations.Add(new Destination { Id = _destination, Slug = "nairobi", Name = "Nairobi", Country = "KE" });
        _db.Properties.Add(new Property { Id = _manor, Slug = "giraffe-manor", Name = "Giraffe Manor", DestinationId = _destination });
        _db.Properties.Add(new Property { Id = _camp, Slug = "salas-camp", Name = "Sala's Camp", DestinationId = _destination });
        _db.Seasons.Add(Season(_peak, "Peak", Today.AddDays(10), Today.AddDays(40)));
        _db.Seasons.Add(Season(_savings, "Savings", Today.AddDays(60), Today.AddDays(90)));
        _db.Seasons.Add(Season(_overlapsPeak, "Overlaps peak", Today.AddDays(30), Today.AddDays(50)));
        _db.Seasons.Add(Season(_ended, "Last year", Today.AddDays(-400), Today.AddDays(-300)));
        _db.Features.Add(new Feature { Id = _champagne, Slug = "champagne", Label = "Champagne breakfast", Icon = "champagne-glasses" });
        _db.SaveChanges();

        var settings = Options.Create(new MediaStorageSettings { RootPath = _root });
        _packages = new AdminPackageService(_db, new LocalDiskMediaStorage(settings, new FakeEnv()), settings);
        _content = new AdminPackageContentService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static Season Season(Guid id, string name, DateOnly start, DateOnly end) => new()
    {
        Id = id, Slug = name.ToLowerInvariant().Replace(' ', '-'), Name = name,
        Ranges = [new SeasonRange { Id = Guid.NewGuid(), SeasonId = id, StartDate = start, EndDate = end }],
    };

    private async Task<Guid> NewPackage(int nights = 3)
    {
        var created = await _packages.CreateAsync(new CreatePackageRequest(
            "Giraffe Manor Escape", _destination, "LODGE", nights, 2, 0, null, "A short summary.", null, null, null));
        return created.Id;
    }

    private static RateInput Rate(Guid season, long price = 848800, string currency = "USD", long? extra = null) =>
        new(season, currency, price, extra);

    [Fact]
    public async Task Reference_lists_what_the_form_picks_from()
    {
        var reference = await _content.ReferenceAsync();

        Assert.Equal(["Giraffe Manor", "Sala's Camp"], reference.Properties.Select(p => p.Name));
        Assert.Equal(4, reference.Seasons.Count);
        Assert.Single(reference.Seasons.First(s => s.Name == "Peak").Ranges);
        Assert.Equal("Champagne breakfast", Assert.Single(reference.Features).Label);
    }

    [Fact]
    public async Task Stays_are_replaced_in_the_order_given()
    {
        var id = await NewPackage();
        await _content.ReplaceStaysAsync(id, [new(_manor, 1, "Garden Suite")]);

        var result = await _content.ReplaceStaysAsync(id, [new(_camp, 2, null), new(_manor, 1, " Garden Suite ")]);

        Assert.Equal(["Sala's Camp", "Giraffe Manor"], result!.Stays.Select(s => s.PropertyName));
        Assert.Equal("Garden Suite", result.Stays[1].RoomType);
        Assert.Equal([0, 1], result.Stays.Select(s => s.SortOrder));
    }

    [Fact]
    public async Task A_stay_needs_a_real_property_and_sensible_nights()
    {
        var id = await NewPackage();
        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceStaysAsync(id, [new(Guid.NewGuid(), 1, null)]));
        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceStaysAsync(id, [new(_manor, 0, null)]));
        Assert.Empty((await _packages.GetAsync(id))!.Stays);
    }

    [Fact]
    public async Task Features_can_be_shared_or_the_packages_own_lines()
    {
        var id = await NewPackage();

        var result = await _content.ReplaceFeaturesAsync(id, [
            new(FeatureSection.Included, _champagne, "ignored", "Served on arrival"),
            new(FeatureSection.Included, null, "  Airport transfers  ", null),
            new(FeatureSection.Excluded, null, "Park fees", null),
        ]);

        Assert.Equal(["Champagne breakfast", "Airport transfers", "Park fees"], result!.Features.Select(f => f.Label));
        Assert.Equal("champagne-glasses", result.Features[0].Icon);
        Assert.Equal("Served on arrival", result.Features[0].Footnote);
        Assert.Null(result.Features[1].FeatureId);
        Assert.Equal("Excluded", result.Features[2].Section);
    }

    [Fact]
    public async Task A_one_off_feature_needs_its_text()
    {
        var id = await NewPackage();
        await Assert.ThrowsAsync<AppException>(() =>
            _content.ReplaceFeaturesAsync(id, [new(FeatureSection.Included, null, "  ", null)]));
        await Assert.ThrowsAsync<AppException>(() =>
            _content.ReplaceFeaturesAsync(id, [new(FeatureSection.Included, Guid.NewGuid(), null, null)]));
    }

    [Fact]
    public async Task Saving_rates_sets_the_from_price_from_seasons_that_have_not_ended()
    {
        var id = await NewPackage();

        var result = await _content.ReplaceRatesAsync(id, [
            Rate(_peak, 900000), Rate(_savings, 700000), Rate(_ended, 100000), Rate(_savings, 50000, "EUR"),
        ]);

        // The ended season and the other currency don't count.
        Assert.Equal(700000, result!.FromPriceMinor);
        Assert.Equal(4, result.Rates.Count);
    }

    [Fact]
    public async Task Rates_in_the_same_season_and_currency_are_rejected()
    {
        var id = await NewPackage();
        var error = await Assert.ThrowsAsync<AppException>(() => _content.ReplaceRatesAsync(id, [Rate(_peak), Rate(_peak, 1000)]));
        Assert.Contains("twice", error.Message);
    }

    [Fact]
    public async Task Overlapping_seasons_in_one_currency_are_rejected_but_fine_in_two()
    {
        var id = await NewPackage();

        var error = await Assert.ThrowsAsync<AppException>(() => _content.ReplaceRatesAsync(id, [Rate(_peak), Rate(_overlapsPeak)]));
        Assert.Contains("overlap", error.Message);

        var result = await _content.ReplaceRatesAsync(id, [Rate(_peak), Rate(_overlapsPeak, 1000, "EUR")]);
        Assert.Equal(2, result!.Rates.Count);
    }

    [Fact]
    public async Task A_rate_needs_a_season_a_currency_and_a_price()
    {
        var id = await NewPackage();
        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceRatesAsync(id, [Rate(Guid.NewGuid())]));
        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceRatesAsync(id, [Rate(_peak, currency: "US")]));
        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceRatesAsync(id, [Rate(_peak, 0)]));
    }

    [Fact]
    public async Task Changing_the_base_currency_moves_the_from_price()
    {
        var id = await NewPackage();
        await _content.ReplaceRatesAsync(id, [Rate(_peak, 900000), Rate(_savings, 80000, "EUR")]);

        var updated = await _packages.UpdateAsync(id, new UpdatePackageRequest(
            null, null, null, null, null, null, null, null, null, null, null, "eur", null, null, null));

        Assert.Equal("EUR", updated!.BaseCurrency);
        Assert.Equal(80000, updated.FromPriceMinor);
    }

    [Fact]
    public async Task Add_ons_keep_their_ids_when_the_list_is_saved_again()
    {
        var id = await NewPackage();
        var first = await _content.ReplaceAddOnsAsync(id, [
            new(null, "Private vehicle", "With a guide", AddOnUnit.PerDay, "USD", 49000),
            new(null, "Airport pickup", null, AddOnUnit.PerStay, "USD", 8000),
        ]);
        var vehicle = first!.AddOns[0];
        var pickup = first.AddOns[1];

        var second = await _content.ReplaceAddOnsAsync(id, [
            new(vehicle.Id, "Private vehicle", "With a guide and a driver", AddOnUnit.PerDay, "usd", 52000),
            new(null, "Champagne", null, AddOnUnit.PerPerson, "USD", 6000),
        ]);

        Assert.Equal(["Private vehicle", "Champagne"], second!.AddOns.Select(a => a.Name));
        Assert.Equal(vehicle.Id, second.AddOns[0].Id);
        Assert.Equal(52000, second.AddOns[0].PriceMinor);
        Assert.Equal("USD", second.AddOns[0].Currency);
        Assert.DoesNotContain(second.AddOns, a => a.Id == pickup.Id);
    }

    [Fact]
    public async Task An_add_on_from_another_package_is_refused()
    {
        var a = await NewPackage();
        var b = await NewPackage();
        var others = await _content.ReplaceAddOnsAsync(b, [new(null, "Theirs", null, AddOnUnit.PerStay, "USD", 100)]);

        await Assert.ThrowsAsync<AppException>(() =>
            _content.ReplaceAddOnsAsync(a, [new(others!.AddOns[0].Id, "Theirs", null, AddOnUnit.PerStay, "USD", 100)]));
    }

    [Fact]
    public async Task Publishing_an_empty_draft_lists_everything_it_still_needs()
    {
        var id = await _packages.CreateAsync(new CreatePackageRequest(
            "Bare", _destination, "LODGE", 2, 2, 0, null, null, null, null, null)).ContinueWith(t => t.Result.Id);

        var outcome = await _content.PublishAsync(id);

        Assert.NotNull(outcome.Detail);
        Assert.Contains("A summary", outcome.Missing);
        Assert.Contains("At least one stay", outcome.Missing);
        Assert.Contains("A USD rate for a season that has not ended", outcome.Missing);
        Assert.Contains("A main photo", outcome.Missing);
        Assert.Equal("Draft", (await _packages.GetAsync(id))!.Status);
    }

    [Fact]
    public async Task A_flight_publishes_with_a_summary_and_a_photo_and_keeps_its_own_price()
    {
        var created = await _packages.CreateAsync(new CreatePackageRequest(
            "Nairobi to Zanzibar", _destination, "", 0, 0, 0, null, null, null, null, null, ProductType.Flight,
            JObject.Parse("""{"origin":"Nairobi","destination":"Zanzibar"}"""), FromPriceMinor: 18000));
        Assert.Equal(18000, created.FromPriceMinor);

        var blocked = await _content.PublishAsync(created.Id);
        Assert.Equal(["A summary", "A main photo"], blocked.Missing);

        await _packages.UpdateAsync(created.Id, new UpdatePackageRequest(
            null, null, null, null, null, null, null, null, "Return fares on the coast run.", null, null, null, null, null, null));
        await _packages.AddPhotosAsync(created.Id, [new UploadedImage("plane.png", 16, () => new MemoryStream(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0]))]);
        var published = await _content.PublishAsync(created.Id);

        Assert.Empty(published.Missing);
        Assert.Equal(18000, published.Detail!.FromPriceMinor); // not recomputed from (absent) rates
    }

    [Fact]
    public async Task Stays_rates_and_add_ons_are_for_holidays_only()
    {
        var visa = await _packages.CreateAsync(new CreatePackageRequest(
            "Kenya eTA", _destination, "", 0, 0, 0, null, null, null, null, null, ProductType.VisaSupport,
            JObject.Parse("""{"country":"Kenya","visaType":"eTA"}""")));

        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceStaysAsync(visa.Id, [new(_manor, 1, null)]));
        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceRatesAsync(visa.Id, [Rate(_peak, 100)]));
        await Assert.ThrowsAsync<AppException>(() => _content.ReplaceAddOnsAsync(visa.Id, []));
    }

    [Fact]
    public async Task Stays_that_do_not_add_up_to_the_nights_block_publishing()
    {
        var id = await NewPackage(nights: 3);
        await _content.ReplaceStaysAsync(id, [new(_manor, 2, null)]);

        var outcome = await _content.PublishAsync(id);

        Assert.Contains(outcome.Missing, m => m.StartsWith("Stays that add up to 3 nights (they add up to 2)"));
    }

    [Fact]
    public async Task A_complete_package_publishes_and_can_be_taken_down()
    {
        var id = await NewPackage(nights: 3);
        await _content.ReplaceStaysAsync(id, [new(_manor, 1, null), new(_camp, 2, null)]);
        await _content.ReplaceRatesAsync(id, [Rate(_peak, 900000)]);
        var png = new UploadedImage("lawn.png", 16, () => new MemoryStream(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0]));
        await _packages.AddPhotosAsync(id, [png]);

        var outcome = await _content.PublishAsync(id);

        Assert.Empty(outcome.Missing);
        Assert.Equal("Published", outcome.Detail!.Status);
        Assert.NotNull(outcome.Detail.PublishedAt);
        Assert.Equal(900000, outcome.Detail.FromPriceMinor);

        var down = await _content.UnpublishAsync(id);
        Assert.Equal("Draft", down!.Status);
        Assert.NotNull(down.PublishedAt); // it keeps the date it first went live

        Assert.Equal("Archived", (await _content.ArchiveAsync(id))!.Status);
    }

    [Fact]
    public async Task The_minimum_stay_cannot_exceed_the_nights()
    {
        var id = await NewPackage(nights: 3);
        var request = new UpdatePackageRequest(null, null, null, null, 5, null, null, null, null, null, null, null, null, null, null);

        await Assert.ThrowsAsync<AppException>(() => _packages.UpdateAsync(id, request));
    }

    [Fact]
    public async Task Missing_packages_return_null_and_never_throw()
    {
        var nobody = Guid.NewGuid();
        Assert.Null(await _content.ReplaceStaysAsync(nobody, []));
        Assert.Null(await _content.ReplaceFeaturesAsync(nobody, []));
        Assert.Null(await _content.ReplaceRatesAsync(nobody, []));
        Assert.Null(await _content.ReplaceAddOnsAsync(nobody, []));
        Assert.Null((await _content.PublishAsync(nobody)).Detail);
        Assert.Null(await _content.UnpublishAsync(nobody));
        Assert.Null(await _content.ArchiveAsync(nobody));
    }

    private sealed class FakeEnv : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
