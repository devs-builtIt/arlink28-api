using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Data.Seed;
using Arlink28.Api.Features.Shared.Services;
using Arlink28.Api.Helpers.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Tests;

/// <summary>The shipped write-ups: they must fit the model, name real destinations, and read as finished.</summary>
public class DestinationContentTests
{
    private static readonly IReadOnlyList<DestinationContent> Content = DestinationContentSeeder.LoadContent();

    [Fact]
    public void Every_entry_names_a_seeded_destination_once()
    {
        var seeded = CatalogueSeedData.Destinations.Select(d => d.Slug).ToHashSet();
        Assert.All(Content, c => Assert.Contains(c.Slug, seeded));
        Assert.Equal(Content.Count, Content.Select(c => c.Slug).Distinct().Count());
    }

    [Fact]
    public void Every_entry_has_the_text_a_page_needs_and_fits_the_columns()
    {
        Assert.All(Content, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Tagline), $"{c.Slug}: tagline");
            Assert.False(string.IsNullOrWhiteSpace(c.Summary), $"{c.Slug}: summary");
            Assert.False(string.IsNullOrWhiteSpace(c.Description), $"{c.Slug}: description");
            Assert.False(string.IsNullOrWhiteSpace(c.BestTimeToVisit), $"{c.Slug}: best time to visit");
            Assert.True(c.Tagline!.Length <= 160, $"{c.Slug}: tagline is {c.Tagline.Length} characters, the limit is 160");
            Assert.True(c.Summary!.Length <= 400, $"{c.Slug}: summary is {c.Summary.Length} characters, the limit is 400");
            Assert.True(c.Description!.Length <= 10000, $"{c.Slug}: description is too long");
            Assert.True(c.BestTimeToVisit!.Length <= 200, $"{c.Slug}: best time to visit is {c.BestTimeToVisit.Length} characters, the limit is 200");
            Assert.Equal(c.Tagline.Trim(), c.Tagline);
            Assert.Equal(c.Summary.Trim(), c.Summary);
        });
    }

    [Fact]
    public void Places_list_at_least_three_things_to_do_and_countries_list_none()
    {
        var places = CatalogueSeedData.Destinations.Where(d => d.Parent is not null).Select(d => d.Slug).ToHashSet();
        foreach (var c in Content)
        {
            var count = c.Attractions?.Count ?? 0;
            if (places.Contains(c.Slug)) Assert.True(count >= 3, $"{c.Slug}: a place needs at least three attractions, has {count}");
            else Assert.Equal(0, count);
        }
    }

    [Fact]
    public void Attractions_fit_the_columns_and_are_not_repeated_within_a_destination()
    {
        foreach (var c in Content)
        {
            var attractions = c.Attractions ?? [];
            Assert.True(attractions.Count <= 50, $"{c.Slug}: too many attractions");
            Assert.Equal(attractions.Count, attractions.Select(a => a.Name).Distinct().Count());
            foreach (var a in attractions)
            {
                Assert.False(string.IsNullOrWhiteSpace(a.Name), $"{c.Slug}: an attraction has no name");
                Assert.True(a.Name.Length <= 120, $"{c.Slug}/{a.Name}: name is too long");
                Assert.False(string.IsNullOrWhiteSpace(a.Summary), $"{c.Slug}/{a.Name}: no summary");
                Assert.True(a.Summary.Length <= 300, $"{c.Slug}/{a.Name}: summary is {a.Summary.Length} characters, the limit is 300");
            }
        }
    }

    [Fact]
    public void Photos_have_a_plain_file_name_alt_text_and_a_credit()
    {
        foreach (var c in Content.Where(c => c.Photo is not null))
        {
            var p = c.Photo!;
            Assert.Equal(Path.GetFileName(p.File), p.File);   // no folders: it must stay inside the photos folder
            Assert.EndsWith(".jpg", p.File);
            Assert.False(string.IsNullOrWhiteSpace(p.Alt), $"{c.Slug}: photo alt text");
            Assert.True(p.Alt.Length <= 250, $"{c.Slug}: photo alt text is too long");
            Assert.StartsWith("Photo by ", p.Credit);
            Assert.True(p.Credit.Length <= 250, $"{c.Slug}: credit is too long");
        }
    }

    [Fact]
    public void Coordinates_come_as_a_pair_and_are_on_the_map()
    {
        Assert.All(Content, c =>
        {
            Assert.Equal(c.Latitude.HasValue, c.Longitude.HasValue);
            if (c.Latitude is { } lat) Assert.InRange(lat, -90, 90);
            if (c.Longitude is { } lng) Assert.InRange(lng, -180, 180);
        });
    }

    [Fact]
    public void The_places_the_site_leads_with_are_covered()
    {
        var slugs = Content.Select(c => c.Slug).ToHashSet();
        foreach (var expected in new[] { "botswana", "zimbabwe", "egypt", "victoria-falls", "chobe-national-park", "okavango-delta", "giza", "luxor", "cairo" })
            Assert.Contains(expected, slugs);
    }
}

public sealed class DestinationContentSeederTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arlink28-seed-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _photos;
    private readonly ApplicationDbContext _db;
    private readonly DestinationContentSeeder _seeder;

    public DestinationContentSeederTests()
    {
        _photos = Path.Combine(_root, "photos");
        Directory.CreateDirectory(_photos);
        File.WriteAllBytes(Path.Combine(_photos, "falls.jpg"), JpegBytes());

        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var settings = Options.Create(new MediaStorageSettings { RootPath = Path.Combine(_root, "media"), MaxFileSizeBytes = 4096 });
        _seeder = new DestinationContentSeeder(_db, new LocalDiskMediaStorage(settings, new FakeEnv()), settings);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static byte[] JpegBytes(int size = 64)
    {
        var bytes = new byte[size];
        new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.CopyTo(bytes, 0);
        return bytes;
    }

    private string[] MediaOnDisk() =>
        Directory.Exists(Path.Combine(_root, "media")) ? Directory.GetFiles(Path.Combine(_root, "media"), "*", SearchOption.AllDirectories) : [];

    private Destination Add(string slug = "chobe", DestinationStatus status = DestinationStatus.Draft)
    {
        var d = new Destination { Id = Guid.NewGuid(), Slug = slug, Name = slug, Country = "BW", Status = status };
        _db.Destinations.Add(d);
        _db.SaveChanges();
        return d;
    }

    private static DestinationContent Entry(string slug = "chobe", string? photoFile = "falls.jpg", params string[] attractions) => new(
        slug, "A tagline", "A summary", "A description", "May to October", -17.9, 25.8,
        photoFile is null ? null : new PhotoContent(photoFile, "Elephants at the river", "Photo by A. Photographer on Unsplash"),
        attractions.Select(a => new AttractionContent(a, $"{a} summary")).ToList());

    private Task<IReadOnlyList<string>> Seed(DestinationContent entry, bool dryRun = false, string? photos = "") =>
        _seeder.SeedAsync([entry], photos == "" ? _photos : photos, dryRun);

    [Fact]
    public async Task Fills_a_bare_destination_in_full_and_leaves_it_a_draft()
    {
        var d = Add();
        var lines = await Seed(Entry("chobe", "falls.jpg", "River safari", "Game drive"));

        var saved = await _db.Destinations.Include(x => x.Attractions).SingleAsync(x => x.Id == d.Id);
        Assert.Equal("A tagline", saved.Tagline);
        Assert.Equal("A summary", saved.Summary);
        Assert.Equal("A description", saved.Description);
        Assert.Equal("May to October", saved.BestTimeToVisit);
        Assert.Equal(-17.9, saved.Latitude);
        Assert.Equal(["River safari", "Game drive"], saved.Attractions.OrderBy(a => a.SortOrder).Select(a => a.Name));
        Assert.StartsWith("/media/destinations/", saved.HeroPath);
        Assert.Equal("Elephants at the river", saved.HeroAlt);
        Assert.Equal("Photo by A. Photographer on Unsplash", saved.HeroCredit);
        Assert.Single(MediaOnDisk());

        // It writes the text, but publishing is a person's decision.
        Assert.Equal(DestinationStatus.Draft, saved.Status);
        Assert.Null(saved.PublishedAt);
        Assert.Contains("filled chobe", lines.Single());
        Assert.Equal("DestinationContentSeeded", (await _db.AuditLogs.SingleAsync()).Action);
    }

    [Fact]
    public async Task Running_it_again_changes_nothing()
    {
        Add();
        var entry = Entry("chobe", "falls.jpg", "River safari");
        await Seed(entry);
        var path = (await _db.Destinations.SingleAsync()).HeroPath;

        var again = await Seed(entry);
        Assert.StartsWith("keep", again.Single());
        Assert.Equal(path, (await _db.Destinations.SingleAsync()).HeroPath);
        Assert.Single(MediaOnDisk());
        Assert.Single(_db.DestinationAttractions);
        Assert.Single(_db.AuditLogs);
    }

    [Fact]
    public async Task Never_overwrites_what_staff_wrote()
    {
        var d = Add();
        d.Summary = "Staff wrote this";
        d.Tagline = "And this";
        d.HeroPath = "/media/destinations/own.jpg";
        d.HeroCredit = "Our own photo";
        _db.DestinationAttractions.Add(new DestinationAttraction { Id = Guid.NewGuid(), DestinationId = d.Id, Name = "Our attraction" });
        _db.SaveChanges();

        var lines = await Seed(Entry("chobe", "falls.jpg", "River safari"));

        var saved = await _db.Destinations.Include(x => x.Attractions).SingleAsync();
        Assert.Equal("Staff wrote this", saved.Summary);
        Assert.Equal("And this", saved.Tagline);
        Assert.Equal("A description", saved.Description);   // the empty fields still fill
        Assert.Equal("/media/destinations/own.jpg", saved.HeroPath);
        Assert.Equal("Our own photo", saved.HeroCredit);
        Assert.Equal(["Our attraction"], saved.Attractions.Select(a => a.Name));
        Assert.Empty(MediaOnDisk());
        Assert.Contains("kept its 1 attractions", lines.Single());
        Assert.Contains("kept its hero photo", lines.Single());
    }

    [Fact]
    public async Task Leaves_a_published_destination_published()
    {
        var published = DateTime.UtcNow.AddDays(-3);
        var d = Add("chobe", DestinationStatus.Published);
        d.PublishedAt = published;
        _db.SaveChanges();

        await Seed(Entry());
        var saved = await _db.Destinations.SingleAsync();
        Assert.Equal(DestinationStatus.Published, saved.Status);
        Assert.Equal(published, saved.PublishedAt);
    }

    [Fact]
    public async Task A_dry_run_reports_what_it_would_do_and_does_nothing()
    {
        Add();
        var lines = await Seed(Entry("chobe", "falls.jpg", "River safari"), dryRun: true);

        Assert.StartsWith("would", lines.Single());
        Assert.Contains("hero photo", lines.Single());
        var d = await _db.Destinations.Include(x => x.Attractions).SingleAsync();
        Assert.Null(d.Summary);
        Assert.Null(d.HeroPath);
        Assert.Empty(d.Attractions);
        Assert.Empty(MediaOnDisk());
        Assert.Empty(_db.AuditLogs);
    }

    [Fact]
    public async Task Without_a_photos_folder_the_text_still_goes_in_and_the_photo_is_explained()
    {
        Add();
        var lines = await _seeder.SeedAsync([Entry()], null, dryRun: false);
        var d = await _db.Destinations.SingleAsync();
        Assert.Equal("A summary", d.Summary);
        Assert.Null(d.HeroPath);
        Assert.Contains("pass --photos=<folder>", lines.Single());
    }

    [Fact]
    public async Task A_photo_that_is_missing_or_not_an_image_or_too_big_is_skipped_with_a_reason()
    {
        Add("a");
        Add("b");
        Add("c");
        File.WriteAllText(Path.Combine(_photos, "notes.jpg"), "just some text here");
        File.WriteAllBytes(Path.Combine(_photos, "huge.jpg"), JpegBytes(8192));

        var lines = await _seeder.SeedAsync([Entry("a", "nope.jpg"), Entry("b", "notes.jpg"), Entry("c", "huge.jpg")], _photos, dryRun: false);

        Assert.Contains("nope.jpg is not in", lines[0]);
        Assert.Contains("not a JPEG, PNG or WebP", lines[1]);
        Assert.Contains("larger than", lines[2]);
        Assert.All(_db.Destinations, d => Assert.Null(d.HeroPath));
        Assert.All(_db.Destinations, d => Assert.Equal("A summary", d.Summary));   // the text went in regardless
        Assert.Empty(MediaOnDisk());
    }

    [Fact]
    public async Task A_destination_that_does_not_exist_is_reported_not_created()
    {
        var lines = await Seed(Entry("no-such-place"));
        Assert.StartsWith("missing no-such-place", lines.Single());
        Assert.Empty(_db.Destinations);
    }

    [Fact]
    public async Task Coordinates_are_filled_only_as_a_pair_and_never_over_existing_ones()
    {
        var d = Add();
        d.Latitude = 1;
        d.Longitude = 2;
        _db.SaveChanges();
        await Seed(Entry());
        var saved = await _db.Destinations.SingleAsync();
        Assert.Equal(1, saved.Latitude);
        Assert.Equal(2, saved.Longitude);
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
