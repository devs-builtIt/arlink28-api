using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminPackages.RequestModels;
using Arlink28.Api.Features.AdminPackages.ResponseModels;
using Arlink28.Api.Features.AdminPackages.Services;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Features.Shared.Services;
using Arlink28.Api.Helpers;
using Arlink28.Api.Helpers.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Arlink28.Api.Tests;

public sealed class AdminPackageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arlink28-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationDbContext _db;
    private readonly AdminPackageService _service;
    private readonly Guid _destinationId = Guid.NewGuid();

    public AdminPackageServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Destinations.Add(new Destination { Id = _destinationId, Slug = "nairobi", Name = "Nairobi", Country = "Kenya" });
        _db.SaveChanges();

        var settings = Options.Create(new MediaStorageSettings { RootPath = _root, MaxFileSizeBytes = 1024 });
        _service = new AdminPackageService(_db, new LocalDiskMediaStorage(settings, new FakeEnv()), settings);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private CreatePackageRequest NewPackage(string title = "Giraffe Manor Escape") =>
        new(title, _destinationId, "safari", 2, 2, 0, null, null, null, null, null);

    private static UploadedImage Jpeg(string name = "lawn.jpg", int size = 64) =>
        Image(name, [0xFF, 0xD8, 0xFF, 0xE0], size);

    private static UploadedImage Png(string name = "porch.png") =>
        Image(name, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 64);

    private static UploadedImage Webp(string name = "view.webp") =>
        Image(name, "RIFF"u8.ToArray().Concat(new byte[4]).Concat("WEBP"u8.ToArray()).ToArray(), 64);

    private static UploadedImage Image(string name, byte[] header, int size)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return new UploadedImage(name, bytes.Length, () => new MemoryStream(bytes));
    }

    private string[] FilesOnDisk() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : [];

    [Fact]
    public async Task Create_makes_a_draft_with_a_unique_slug_and_upper_case_category()
    {
        var first = await _service.CreateAsync(NewPackage());
        var second = await _service.CreateAsync(NewPackage());

        Assert.Equal("Draft", first.Status);
        Assert.Equal("SAFARI", first.Category);
        Assert.Equal("giraffe-manor-escape", first.Slug);
        Assert.Equal("giraffe-manor-escape-2", second.Slug);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_destination()
    {
        var request = NewPackage() with { DestinationId = Guid.NewGuid() };
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(request));
    }

    [Theory]
    [InlineData("Sala's Camp — Maasai Mara", "sala-s-camp-maasai-mara")]
    [InlineData("  Café Été  ", "cafe-ete")]
    [InlineData("!!!", "package")]
    public void Slugify_produces_url_safe_slugs(string title, string expected) =>
        Assert.Equal(expected, AdminPackageService.Slugify(title));

    [Fact]
    public async Task Several_photos_upload_in_one_request_and_the_first_becomes_the_hero()
    {
        var package = await _service.CreateAsync(NewPackage());

        var saved = await _service.AddPhotosAsync(package.Id, [Jpeg(), Png(), Webp()]);

        Assert.NotNull(saved);
        Assert.Equal(["Hero", "Gallery", "Gallery"], saved!.Select(m => m.Role));
        Assert.Equal([0, 1, 2], saved.Select(m => m.SortKey));
        Assert.All(saved, m => Assert.StartsWith($"/media/packages/{package.Id:N}/", m.Path));
        Assert.Equal(3, FilesOnDisk().Length);
        Assert.Equal("lawn", saved[0].Alt);
    }

    [Fact]
    public async Task A_later_upload_adds_to_the_gallery_and_never_a_second_hero()
    {
        var package = await _service.CreateAsync(NewPackage());
        await _service.AddPhotosAsync(package.Id, [Jpeg()]);

        var more = await _service.AddPhotosAsync(package.Id, [Png(), Png("second.png")]);

        Assert.All(more!, m => Assert.Equal("Gallery", m.Role));
        Assert.Equal([1, 2], more!.Select(m => m.SortKey));
        Assert.Equal(1, (await _service.GetAsync(package.Id))!.Media.Count(m => m.Role == "Hero"));
    }

    [Fact]
    public async Task One_bad_file_rejects_the_whole_upload_and_writes_nothing()
    {
        var package = await _service.CreateAsync(NewPackage());
        var notAnImage = new UploadedImage("notes.jpg", 20, () => new MemoryStream("just some text here"u8.ToArray()));

        var error = await Assert.ThrowsAsync<AppException>(() => _service.AddPhotosAsync(package.Id, [Jpeg(), notAnImage]));

        Assert.Contains("notes.jpg", error.Message);
        Assert.Empty(FilesOnDisk());
        Assert.Empty((await _service.GetAsync(package.Id))!.Media);
    }

    [Fact]
    public async Task A_renamed_file_is_judged_by_its_bytes_not_its_extension()
    {
        var package = await _service.CreateAsync(NewPackage());
        var script = new UploadedImage("photo.jpg", 12, () => new MemoryStream("<script></script>"u8.ToArray()));

        await Assert.ThrowsAsync<AppException>(() => _service.AddPhotosAsync(package.Id, [script]));
    }

    [Fact]
    public async Task An_oversized_photo_is_rejected()
    {
        var package = await _service.CreateAsync(NewPackage());
        var error = await Assert.ThrowsAsync<AppException>(() => _service.AddPhotosAsync(package.Id, [Jpeg(size: 2048)]));
        Assert.Contains("larger than", error.Message);
    }

    [Fact]
    public async Task Uploading_to_a_missing_package_returns_null()
    {
        Assert.Null(await _service.AddPhotosAsync(Guid.NewGuid(), [Jpeg()]));
    }

    [Fact]
    public async Task Making_a_photo_the_hero_demotes_the_old_one()
    {
        var package = await _service.CreateAsync(NewPackage());
        var saved = await _service.AddPhotosAsync(package.Id, [Jpeg(), Png()]);

        await _service.UpdateMediaAsync(package.Id, saved![1].Id, new UpdateMediaRequest(MediaRole.Hero, null, null));

        var media = (await _service.GetAsync(package.Id))!.Media;
        Assert.Equal("Gallery", media.Single(m => m.Id == saved[0].Id).Role);
        Assert.Equal("Hero", media.Single(m => m.Id == saved[1].Id).Role);
    }

    [Fact]
    public async Task Deleting_the_hero_promotes_the_next_photo_and_removes_the_file()
    {
        var package = await _service.CreateAsync(NewPackage());
        var saved = await _service.AddPhotosAsync(package.Id, [Jpeg(), Png(), Webp()]);

        Assert.True(await _service.DeleteMediaAsync(package.Id, saved![0].Id));

        var media = (await _service.GetAsync(package.Id))!.Media;
        Assert.Equal(2, media.Count);
        Assert.Equal(saved[1].Id, media.Single(m => m.Role == "Hero").Id);
        Assert.Equal(2, FilesOnDisk().Length);
        Assert.False(await _service.DeleteMediaAsync(package.Id, saved[0].Id));
    }

    [Fact]
    public async Task Reordering_needs_every_photo_exactly_once()
    {
        var package = await _service.CreateAsync(NewPackage());
        var saved = await _service.AddPhotosAsync(package.Id, [Jpeg(), Png(), Webp()]);
        var ids = saved!.Select(m => m.Id).ToList();

        var reordered = await _service.ReorderMediaAsync(package.Id, new ReorderMediaRequest([ids[2], ids[0], ids[1]]));
        Assert.Equal([ids[2], ids[0], ids[1]], reordered!.Select(m => m.Id));

        await Assert.ThrowsAsync<AppException>(() =>
            _service.ReorderMediaAsync(package.Id, new ReorderMediaRequest([ids[0], ids[1]])));
        await Assert.ThrowsAsync<AppException>(() =>
            _service.ReorderMediaAsync(package.Id, new ReorderMediaRequest([ids[0], ids[0], ids[1]])));
    }

    [Fact]
    public async Task Update_changes_only_the_fields_sent_and_keeps_the_slug()
    {
        var package = await _service.CreateAsync(NewPackage());

        var updated = await _service.UpdateAsync(package.Id, new UpdatePackageRequest(
            "Giraffe Manor Grand Escape", null, null, 4, null, null, null, "Two nights", null, null, null, null, true, null, null));

        Assert.Equal("Giraffe Manor Grand Escape", updated!.Title);
        Assert.Equal(package.Slug, updated.Slug);
        Assert.Equal(4, updated.Nights);
        Assert.Equal("Two nights", updated.Subtitle);
        Assert.Equal("SAFARI", updated.Category);
        Assert.True(updated.Featured);
    }

    private CreatePackageRequest NewFlight(JObject? details, string title = "Nairobi to Zanzibar") =>
        new(title, _destinationId, "", 0, 0, 0, null, null, null, null, null, ProductType.Flight, details);

    [Fact]
    public async Task Packages_default_to_holidays_and_keep_no_details()
    {
        var created = await _service.CreateAsync(NewPackage());

        Assert.Equal("HolidayPackage", created.ProductType);
        Assert.Null(created.Details);
    }

    [Fact]
    public async Task A_flight_stores_its_details_and_is_labelled_by_its_type()
    {
        var created = await _service.CreateAsync(NewFlight(JObject.Parse(
            """{"origin":"Nairobi","destination":"Zanzibar","tripType":"Return","cabin":"Economy","ignored":"x"}""")));

        Assert.Equal("Flight", created.ProductType);
        Assert.Equal("FLIGHT", created.Category);
        Assert.Equal("Zanzibar", (string?)created.Details!["destination"]);
        Assert.Equal("Return", (string?)created.Details["tripType"]);
        Assert.Null(created.Details["ignored"]); // unknown fields are dropped
        Assert.Equal("Zanzibar", (string?)(await _service.GetAsync(created.Id))!.Details!["destination"]);
    }

    [Fact]
    public async Task Details_that_do_not_fit_the_type_are_rejected()
    {
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(NewFlight(null)));
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(NewFlight(JObject.Parse("""{"origin":"Nairobi"}"""))));
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(NewFlight(
            JObject.Parse("""{"origin":"A","destination":"B","cabin":"Sleeper"}"""))));
    }

    [Fact]
    public async Task Details_can_be_replaced_but_not_added_to_a_holiday()
    {
        var flight = await _service.CreateAsync(NewFlight(JObject.Parse("""{"origin":"Nairobi","destination":"Zanzibar"}""")));
        var holiday = await _service.CreateAsync(NewPackage());

        var updated = await _service.UpdateAsync(flight.Id, new UpdatePackageRequest(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            JObject.Parse("""{"origin":"Nairobi","destination":"Mombasa"}""")));

        Assert.Equal("Mombasa", (string?)updated!.Details!["destination"]);
        await Assert.ThrowsAsync<AppException>(() => _service.UpdateAsync(holiday.Id, new UpdatePackageRequest(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, JObject.Parse("{}"))));
    }

    [Fact]
    public async Task A_holiday_still_needs_its_nights_and_a_visa_needs_a_country()
    {
        var holiday = await _service.CreateAsync(NewPackage());
        await Assert.ThrowsAsync<AppException>(() => _service.UpdateAsync(holiday.Id, new UpdatePackageRequest(
            null, null, null, 0, null, null, null, null, null, null, null, null, null, null, null)));

        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(new CreatePackageRequest(
            "Kenya eTA", _destinationId, "", 0, 0, 0, null, null, null, null, null, ProductType.VisaSupport,
            JObject.Parse("""{"visaType":"eTA"}"""))));
        var visa = await _service.CreateAsync(new CreatePackageRequest(
            "Kenya eTA", _destinationId, "", 0, 0, 0, null, null, null, null, null, ProductType.VisaSupport,
            JObject.Parse("""{"country":"Kenya","visaType":"eTA","serviceFeeMinor":2500,"requirements":["Passport"]}""")));
        Assert.Equal(2500, (long?)visa.Details!["serviceFeeMinor"]);
    }

    [Fact]
    public async Task A_private_charter_tier_needs_a_name_and_what_it_includes()
    {
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(new CreatePackageRequest(
            "ARLink28 Elite", _destinationId, "", 0, 0, 0, null, null, null, null, null, ProductType.PrivateCharter,
            JObject.Parse("""{"tier":"Elite"}"""))));
        var tier = await _service.CreateAsync(new CreatePackageRequest(
            "ARLink28 Elite", _destinationId, "", 0, 0, 0, null, null, null, null, null, ProductType.PrivateCharter,
            JObject.Parse("""{"tier":"Elite","includes":["Private aircraft charter","VIP ground transfer"],"unknown":1}""")));
        Assert.Equal("PrivateCharter", tier.ProductType);
        Assert.Equal(2, ((Newtonsoft.Json.Linq.JArray)tier.Details!["includes"]!).Count);
        Assert.Null(tier.Details["unknown"]);
    }

    [Fact]
    public async Task The_admin_list_can_be_filtered_by_type()
    {
        await _service.CreateAsync(NewPackage());
        await _service.CreateAsync(NewFlight(JObject.Parse("""{"origin":"Nairobi","destination":"Zanzibar"}""")));

        Assert.Equal(2, (await _service.ListAsync(null, null, null, null, 1, 25)).Items.Count);
        Assert.Equal("Nairobi to Zanzibar", (await _service.ListAsync(null, null, null, null, 1, 25, "flight")).Items.Single().Title);
        Assert.Equal("HolidayPackage", (await _service.ListAsync(null, null, null, null, 1, 25, "HolidayPackage")).Items.Single().ProductType);
    }

    [Fact]
    public async Task The_list_includes_drafts_and_finds_packages_by_name()
    {
        await _service.CreateAsync(NewPackage("Sasaab Lodge Retreat"));
        await _service.CreateAsync(NewPackage("Sala's Camp"));

        Assert.Equal(2, (await _service.ListAsync(null, null, null, null, 1, 25)).Items.Count);
        Assert.Equal(2, (await _service.ListAsync("draft", null, null, null, 1, 25)).Items.Count);
        Assert.Empty((await _service.ListAsync("published", null, null, null, 1, 25)).Items);
        Assert.Equal("Sasaab Lodge Retreat", (await _service.ListAsync(null, "SASAAB", null, null, 1, 25)).Items.Single().Title);
    }

    [Fact]
    public async Task The_list_is_paged_with_totals_and_counts_for_every_status()
    {
        for (var i = 1; i <= 7; i++) await _service.CreateAsync(NewPackage($"Package {i:00}"));
        var first = await _service.CreateAsync(NewPackage("Live one"));
        (await _db.Packages.FindAsync(first.Id))!.Status = PackageStatus.Published;
        await _db.SaveChangesAsync();

        var page1 = await _service.ListAsync(null, null, null, null, 1, 3);
        var page3 = await _service.ListAsync(null, null, null, null, 3, 3);
        var past = await _service.ListAsync(null, null, null, null, 9, 3);

        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(8, page1.Total);
        Assert.Equal((1, 3), (page1.Page, page1.PageSize));
        Assert.Equal(2, page3.Items.Count); // 8 packages: 3 + 3 + 2
        Assert.Empty(past.Items);
        Assert.Equal(8, past.Total);

        // Every page is different, and together they cover everything once.
        var all = new List<Guid>();
        for (var n = 1; n <= 3; n++) all.AddRange((await _service.ListAsync(null, null, null, null, n, 3)).Items.Select(i => i.Id));
        Assert.Equal(8, all.Distinct().Count());

        // The status tabs keep their counts whichever tab is open.
        var drafts = await _service.ListAsync("draft", null, null, null, 1, 25);
        Assert.Equal(7, drafts.Total);
        Assert.Equal(new StatusCounts(8, 7, 1, 0), drafts.Counts);
    }

    [Fact]
    public async Task The_list_filters_by_destination_and_type_and_the_totals_follow()
    {
        var other = Guid.NewGuid();
        _db.Destinations.Add(new Destination { Id = other, Slug = "samburu", Name = "Samburu", Country = "KE" });
        await _db.SaveChangesAsync();
        await _service.CreateAsync(NewPackage("Mara safari") with { Category = "safari" });
        await _service.CreateAsync(NewPackage("Mara lodge") with { Category = "lodge" });
        await _service.CreateAsync(NewPackage("Samburu safari") with { DestinationId = other, Category = "safari" });

        var mara = await _service.ListAsync(null, null, "nairobi", null, 1, 25);
        var safaris = await _service.ListAsync(null, null, null, "safari", 1, 25);
        var samburuLodges = await _service.ListAsync(null, null, "samburu", "lodge", 1, 25);

        Assert.Equal(2, mara.Total);
        Assert.Equal(2, safaris.Total);
        Assert.Equal(2, safaris.Counts.All); // the counts follow the filters, not the tab
        Assert.Equal(0, samburuLodges.Total);
    }

    [Fact]
    public async Task The_page_size_and_page_are_kept_in_range()
    {
        await _service.CreateAsync(NewPackage());

        var result = await _service.ListAsync(null, null, null, null, -4, 5000);

        Assert.Equal((1, 100), (result.Page, result.PageSize));
        Assert.Single(result.Items);
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
