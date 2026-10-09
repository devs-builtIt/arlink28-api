using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminDestinations.RequestModels;
using Arlink28.Api.Features.AdminDestinations.Services;
using Arlink28.Api.Features.AdminPackages.Services.Interfaces;
using Arlink28.Api.Features.Shared.Services;
using Arlink28.Api.Helpers;
using Arlink28.Api.Helpers.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Tests;

public sealed class AdminDestinationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arlink28-dest-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationDbContext _db;
    private readonly AdminDestinationService _service;

    public AdminDestinationServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var settings = Options.Create(new MediaStorageSettings { RootPath = _root, MaxFileSizeBytes = 1024 });
        _service = new AdminDestinationService(_db, new LocalDiskMediaStorage(settings, new FakeEnv()), settings);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static UploadedImage Jpeg(string name = "falls.jpg", int size = 64) => Image(name, [0xFF, 0xD8, 0xFF, 0xE0], size);

    private static UploadedImage Image(string name, byte[] header, int size)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return new UploadedImage(name, bytes.Length, () => new MemoryStream(bytes));
    }

    private string[] FilesOnDisk() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : [];

    private async Task<Guid> NewCountry(string name = "Botswana", string code = "BW") =>
        (await _service.CreateAsync(new CreateDestinationRequest(name, code), "actor")).Id;

    private async Task<Guid> NewPlace(Guid parent, string name = "Chobe National Park") =>
        (await _service.CreateAsync(new CreateDestinationRequest(name, ParentId: parent), "actor")).Id;

    private static AttractionInput Attraction(string name, Guid? id = null) => new(id, name, "A summary");

    // ── create ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_makes_a_draft_country_with_a_slug_from_the_name()
    {
        var country = await _service.CreateAsync(new CreateDestinationRequest("  South Africa ", "za"), "actor");
        Assert.Equal("south-africa", country.Slug);
        Assert.Equal("ZA", country.Country);
        Assert.Equal("Country", country.Kind);
        Assert.Equal("Draft", country.Status);
        Assert.Null(country.PublishedAt);
    }

    [Fact]
    public async Task Create_a_place_copies_the_parents_country_code()
    {
        var parent = await NewCountry();
        var place = await _service.CreateAsync(new CreateDestinationRequest("Chobe National Park", ParentId: parent), "actor");
        Assert.Equal("Place", place.Kind);
        Assert.Equal("BW", place.Country);
        Assert.Equal("Botswana", place.ParentName);
    }

    [Fact]
    public async Task Create_rejects_a_country_without_a_code_and_a_mismatched_code()
    {
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(new CreateDestinationRequest("Egypt"), "actor"));
        var parent = await NewCountry();
        await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateAsync(new CreateDestinationRequest("Chobe", "ZW", ParentId: parent), "actor"));
    }

    [Fact]
    public async Task Create_rejects_a_place_inside_a_place_and_an_unknown_parent()
    {
        var place = await NewPlace(await NewCountry());
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(new CreateDestinationRequest("Lodge", ParentId: place), "actor"));
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(new CreateDestinationRequest("Lodge", ParentId: Guid.NewGuid()), "actor"));
    }

    [Fact]
    public async Task Create_numbers_a_repeated_name_but_conflicts_on_an_explicit_slug()
    {
        await NewCountry("Egypt", "EG");
        var second = await _service.CreateAsync(new CreateDestinationRequest("Egypt", "EG"), "actor");
        Assert.Equal("egypt-2", second.Slug);
        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(new CreateDestinationRequest("Anything", "EG", Slug: "egypt"), "actor"));
    }

    [Fact]
    public async Task Create_writes_an_audit_entry_for_the_actor()
    {
        var id = await NewCountry();
        var entry = await _db.AuditLogs.SingleAsync(a => a.EntityId == id.ToString());
        Assert.Equal("DestinationCreated", entry.Action);
        Assert.Equal("actor", entry.ActorId);
    }

    // ── update ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_changes_only_what_is_sent_and_an_empty_string_clears()
    {
        var id = await NewCountry();
        await _service.UpdateAsync(id, new UpdateDestinationRequest(Tagline: "Wild and wide", Summary: "Delta and desert"), "actor");
        var updated = await _service.UpdateAsync(id, new UpdateDestinationRequest(Tagline: ""), "actor");
        Assert.Null(updated!.Tagline);
        Assert.Equal("Delta and desert", updated.Summary);
        Assert.Equal("Botswana", updated.Name);
    }

    [Fact]
    public async Task Update_slug_is_free_while_a_draft_and_locked_once_published()
    {
        var country = await NewCountry();
        var renamed = await _service.UpdateAsync(country, new UpdateDestinationRequest(Slug: "bots"), "actor");
        Assert.Equal("bots", renamed!.Slug);

        var other = await NewCountry("Egypt", "EG");
        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(other, new UpdateDestinationRequest(Slug: "bots"), "actor"));

        var place = await NewPlace(country);
        await ReadyAndPublish(place);
        await _service.UnpublishAsync(place, "actor");   // unpublishing does not unlock the address
        var locked = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(place, new UpdateDestinationRequest(Slug: "somewhere-else"), "actor"));
        Assert.Contains("can't change", locked.Message);
    }

    [Fact]
    public async Task Update_cannot_blank_the_summary_of_a_published_destination()
    {
        var place = await NewPlace(await NewCountry());
        await ReadyAndPublish(place);
        await Assert.ThrowsAsync<AppException>(() => _service.UpdateAsync(place, new UpdateDestinationRequest(Summary: ""), "actor"));
    }

    [Fact]
    public async Task Missing_destination_gives_null_everywhere()
    {
        var nobody = Guid.NewGuid();
        Assert.Null(await _service.GetAsync(nobody));
        Assert.Null(await _service.UpdateAsync(nobody, new UpdateDestinationRequest(Name: "x"), "a"));
        Assert.Null(await _service.ReplaceAttractionsAsync(nobody, [], "a"));
        Assert.Null(await _service.SetHeroAsync(nobody, Jpeg(), null, null, "a"));
        Assert.Null((await _service.PublishAsync(nobody, "a")).Detail);
        Assert.Null(await _service.UnpublishAsync(nobody, "a"));
        Assert.False(await _service.DeleteAsync(nobody, "a"));
    }

    // ── attractions ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Attractions_are_replaced_in_list_order_and_keep_their_photos_by_id()
    {
        var place = await NewPlace(await NewCountry());
        var first = await _service.ReplaceAttractionsAsync(place, [Attraction("River safari"), Attraction("Elephant crossing")], "actor");
        var river = first!.Attractions.Single(a => a.Name == "River safari");
        await _service.SetAttractionPhotoAsync(place, river.Id, Jpeg(), "Boat on the river", "Photo by A", "actor");

        var reordered = await _service.ReplaceAttractionsAsync(place,
            [Attraction("Elephant crossing"), new AttractionInput(river.Id, "River safari", "Updated", "Boat on the river", "Photo by A")], "actor");

        Assert.Equal(["Elephant crossing", "River safari"], reordered!.Attractions.Select(a => a.Name));
        var kept = reordered.Attractions.Single(a => a.Name == "River safari");
        Assert.Equal(river.Id, kept.Id);
        Assert.NotNull(kept.PhotoPath);
        Assert.Equal("Updated", kept.Summary);
    }

    [Fact]
    public async Task Attractions_left_out_are_deleted_with_their_photo_files()
    {
        var place = await NewPlace(await NewCountry());
        var saved = await _service.ReplaceAttractionsAsync(place, [Attraction("River safari"), Attraction("Quad bikes")], "actor");
        var river = saved!.Attractions[0];
        await _service.SetAttractionPhotoAsync(place, river.Id, Jpeg(), null, null, "actor");
        Assert.Single(FilesOnDisk());

        var after = await _service.ReplaceAttractionsAsync(place, [Attraction("Quad bikes", saved.Attractions[1].Id)], "actor");
        Assert.Single(after!.Attractions);
        Assert.Empty(FilesOnDisk());
    }

    [Fact]
    public async Task Attractions_reject_foreign_and_duplicate_ids_and_too_many()
    {
        var a = await NewPlace(await NewCountry());
        var b = await NewPlace(await NewCountry("Egypt", "EG"), "Giza");
        var other = (await _service.ReplaceAttractionsAsync(b, [Attraction("Pyramids")], "actor"))!.Attractions[0];

        await Assert.ThrowsAsync<AppException>(() => _service.ReplaceAttractionsAsync(a, [Attraction("x", other.Id)], "actor"));

        var mine = (await _service.ReplaceAttractionsAsync(a, [Attraction("One")], "actor"))!.Attractions[0];
        await Assert.ThrowsAsync<AppException>(() =>
            _service.ReplaceAttractionsAsync(a, [Attraction("One", mine.Id), Attraction("One", mine.Id)], "actor"));

        var tooMany = Enumerable.Range(0, 51).Select(i => Attraction($"Thing {i}")).ToList();
        await Assert.ThrowsAsync<AppException>(() => _service.ReplaceAttractionsAsync(a, tooMany, "actor"));
    }

    [Fact]
    public async Task A_published_place_cannot_lose_all_its_attractions()
    {
        var place = await NewPlace(await NewCountry());
        await ReadyAndPublish(place);
        await Assert.ThrowsAsync<AppException>(() => _service.ReplaceAttractionsAsync(place, [], "actor"));
    }

    // ── photos ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hero_upload_stores_the_file_defaults_alt_to_the_name_and_replaces_the_old_file()
    {
        var id = await NewCountry();
        var first = await _service.SetHeroAsync(id, Jpeg(), null, "Photo by A on Unsplash", "actor");
        Assert.StartsWith("/media/destinations/", first!.HeroPath);
        Assert.Equal("Botswana", first.HeroAlt);
        Assert.Equal("Photo by A on Unsplash", first.HeroCredit);
        Assert.Single(FilesOnDisk());

        var second = await _service.SetHeroAsync(id, Jpeg("other.jpg"), "Elephants at the river", null, "actor");
        Assert.Single(FilesOnDisk());
        Assert.NotEqual(first.HeroPath, second!.HeroPath);
        Assert.Equal("Elephants at the river", second.HeroAlt);
        Assert.Null(second.HeroCredit);   // the credit belonged to the old photo
    }

    [Fact]
    public async Task Hero_upload_rejects_non_images_oversize_and_empty_files_and_leaves_nothing_behind()
    {
        var id = await NewCountry();
        var text = new UploadedImage("notes.jpg", 20, () => new MemoryStream("just some text here"u8.ToArray()));
        await Assert.ThrowsAsync<AppException>(() => _service.SetHeroAsync(id, text, null, null, "actor"));
        await Assert.ThrowsAsync<AppException>(() => _service.SetHeroAsync(id, Jpeg(size: 5000), null, null, "actor"));
        await Assert.ThrowsAsync<AppException>(() =>
            _service.SetHeroAsync(id, new UploadedImage("empty.jpg", 0, () => new MemoryStream()), null, null, "actor"));
        Assert.Empty(FilesOnDisk());
        Assert.Null((await _service.GetAsync(id))!.HeroPath);
    }

    [Fact]
    public async Task Attraction_photo_for_an_unknown_attraction_is_null()
    {
        var place = await NewPlace(await NewCountry());
        Assert.Null(await _service.SetAttractionPhotoAsync(place, Guid.NewGuid(), Jpeg(), null, null, "actor"));
        Assert.Empty(FilesOnDisk());
    }

    // ── publish ───────────────────────────────────────────────────────────────────────────────

    private async Task ReadyAndPublish(Guid placeId)
    {
        await _service.UpdateAsync(placeId, new UpdateDestinationRequest(Summary: "A short summary"), "actor");
        await _service.SetHeroAsync(placeId, Jpeg(), null, null, "actor");
        await _service.ReplaceAttractionsAsync(placeId, [Attraction("Something to do")], "actor");
        var outcome = await _service.PublishAsync(placeId, "actor");
        Assert.Empty(outcome.Missing);
    }

    [Fact]
    public async Task Publish_lists_everything_a_place_is_missing()
    {
        var place = await NewPlace(await NewCountry());
        var outcome = await _service.PublishAsync(place, "actor");
        Assert.Equal(["A hero photo", "A summary", "At least one attraction"], outcome.Missing);
        Assert.Equal("Draft", outcome.Detail!.Status);
    }

    [Fact]
    public async Task A_country_needs_a_published_place_before_it_can_publish()
    {
        var country = await NewCountry();
        await _service.UpdateAsync(country, new UpdateDestinationRequest(Summary: "Delta and desert"), "actor");
        await _service.SetHeroAsync(country, Jpeg(), null, null, "actor");
        Assert.Equal(["At least one published place"], (await _service.PublishAsync(country, "actor")).Missing);

        var place = await NewPlace(country);
        Assert.Equal(["At least one published place"], (await _service.PublishAsync(country, "actor")).Missing);   // a draft place does not count
        await ReadyAndPublish(place);

        var outcome = await _service.PublishAsync(country, "actor");
        Assert.Empty(outcome.Missing);
        Assert.Equal("Published", outcome.Detail!.Status);
    }

    [Fact]
    public async Task Publish_stamps_the_first_time_and_unpublish_returns_to_draft()
    {
        var place = await NewPlace(await NewCountry());
        await ReadyAndPublish(place);
        var live = (await _service.GetAsync(place))!;
        Assert.True(live.SlugLocked);
        var stamp = live.PublishedAt;

        var draft = await _service.UnpublishAsync(place, "actor");
        Assert.Equal("Draft", draft!.Status);
        await _service.PublishAsync(place, "actor");
        Assert.Equal(stamp, (await _service.GetAsync(place))!.PublishedAt);
    }

    // ── delete ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_removes_a_destination_nothing_uses_along_with_its_files()
    {
        var place = await NewPlace(await NewCountry());
        await _service.SetHeroAsync(place, Jpeg(), null, null, "actor");
        var attraction = (await _service.ReplaceAttractionsAsync(place, [Attraction("River safari")], "actor"))!.Attractions[0];
        await _service.SetAttractionPhotoAsync(place, attraction.Id, Jpeg("two.jpg"), null, null, "actor");
        Assert.Equal(2, FilesOnDisk().Length);

        Assert.True(await _service.DeleteAsync(place, "actor"));
        Assert.Null(await _service.GetAsync(place));
        Assert.Empty(FilesOnDisk());
        Assert.Empty(_db.DestinationAttractions);
    }

    [Fact]
    public async Task Delete_is_a_conflict_while_places_packages_or_properties_use_it()
    {
        var country = await NewCountry();
        var place = await NewPlace(country);
        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(country, "actor"));

        _db.Packages.Add(new Package
        {
            Id = Guid.NewGuid(), Slug = "chobe-safari", Title = "Chobe", Category = "SAFARI", BaseCurrency = "USD",
            DestinationId = place, Nights = 3, MinNights = 3, Adults = 2,
        });
        await _db.SaveChangesAsync();
        var inUse = await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(place, "actor"));
        Assert.Contains("Packages", inUse.Message);

        _db.Packages.RemoveRange(_db.Packages);
        _db.Properties.Add(new Property { Id = Guid.NewGuid(), Slug = "camp", Name = "Camp", DestinationId = place });
        await _db.SaveChangesAsync();
        var withProperty = await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(place, "actor"));
        Assert.Contains("Properties", withProperty.Message);
        Assert.NotNull(await _service.GetAsync(place));
    }

    // ── list ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_filters_searches_counts_by_status_and_pages()
    {
        var bw = await NewCountry();
        var chobe = await NewPlace(bw);
        await NewPlace(bw, "Okavango Delta");
        await NewCountry("Egypt", "EG");
        await ReadyAndPublish(chobe);

        var all = await _service.ListAsync(null, null, null, null, 1, 25);
        Assert.Equal(4, all.Total);
        Assert.Equal(new(4, 3, 1), all.Counts);

        var published = await _service.ListAsync("published", null, null, null, 1, 25);
        Assert.Equal(["Chobe National Park"], published.Items.Select(i => i.Name));
        Assert.Equal(1, published.Items[0].AttractionCount);
        Assert.Equal(4, published.Counts.All);   // tabs keep counting the other statuses

        Assert.Equal(["Egypt"], (await _service.ListAsync(null, null, "eg", null, 1, 25)).Items.Select(i => i.Name));
        Assert.Equal(["Okavango Delta"], (await _service.ListAsync(null, "okavango", null, "place", 1, 25)).Items.Select(i => i.Name));
        Assert.Equal(2, (await _service.ListAsync(null, null, "BW", "Place", 1, 25)).Total);

        var page = await _service.ListAsync(null, null, null, null, 2, 3);
        Assert.Single(page.Items);
        Assert.Equal(4, page.Total);
    }

    [Fact]
    public async Task Detail_lists_all_places_including_drafts_with_a_package_count_and_a_checklist()
    {
        var country = await NewCountry();
        var place = await NewPlace(country);
        await NewPlace(country, "Okavango Delta");
        _db.Packages.Add(new Package
        {
            Id = Guid.NewGuid(), Slug = "chobe-safari", Title = "Chobe", Category = "SAFARI", BaseCurrency = "USD",
            DestinationId = place, Nights = 3, MinNights = 3, Adults = 2, Status = PackageStatus.Draft,
        });
        await _db.SaveChangesAsync();

        var detail = (await _service.GetAsync(country))!;
        Assert.Equal(2, detail.Places.Count);
        Assert.Equal(1, detail.PackageCount);
        Assert.Contains("A hero photo", detail.Missing);
        Assert.False(detail.SlugLocked);
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
