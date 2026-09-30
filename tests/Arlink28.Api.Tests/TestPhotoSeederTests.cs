using System.Net;
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

public sealed class TestPhotoSeederTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arlink28-photos-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationDbContext _db;
    private readonly LocalDiskMediaStorage _storage;
    private readonly Guid _nairobi = Guid.NewGuid();
    private readonly Guid _mara = Guid.NewGuid();
    private readonly List<string> _requested = [];
    private int _failOnRequest = -1;

    public TestPhotoSeederTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Destinations.Add(new Destination { Id = _nairobi, Slug = "nairobi", Name = "Nairobi", Country = "KE" });
        _db.Destinations.Add(new Destination { Id = _mara, Slug = "masai-mara", Name = "Masai Mara", Country = "KE" });
        _db.SaveChanges();
        _storage = new LocalDiskMediaStorage(Options.Create(new MediaStorageSettings { RootPath = _root }), new FakeEnv());
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private TestPhotoSeeder Seeder() => new(_db, _storage, new HttpClient(new StubHandler(this)));

    private sealed class StubHandler(TestPhotoSeederTests owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            owner._requested.Add(request.RequestUri!.ToString());
            if (owner._requested.Count == owner._failOnRequest)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            var jpeg = new byte[64];
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.CopyTo(jpeg, 0);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(jpeg) });
        }
    }

    private Package Add(string slug, Guid destination, params PackageMedia[] media)
    {
        var package = new Package
        {
            Id = Guid.NewGuid(), Slug = slug, Title = slug, Status = PackageStatus.Published, Category = "SAFARI",
            DestinationId = destination, Nights = 2, MinNights = 2, Adults = 2, BaseCurrency = "USD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.Packages.Add(package);
        foreach (var m in media) { m.PackageId = package.Id; _db.PackageMedia.Add(m); }
        _db.SaveChanges();
        return package;
    }

    private static PackageMedia Poster(string path = "/images/packages/flyer.jpg") =>
        new() { Id = Guid.NewGuid(), Role = MediaRole.Hero, Path = path, SortKey = 0, CreatedAt = DateTime.UtcNow };

    private string[] FilesOnDisk() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories) : [];

    [Fact]
    public async Task A_package_with_no_photos_gets_four_the_first_as_its_hero()
    {
        var package = Add("sala-mara-escape", _mara);

        await Seeder().SeedAsync(4, dryRun: false);

        var media = await _db.PackageMedia.Where(m => m.PackageId == package.Id).OrderBy(m => m.SortKey).ToListAsync();
        Assert.Equal(4, media.Count);
        Assert.Equal([MediaRole.Hero, MediaRole.Gallery, MediaRole.Gallery, MediaRole.Gallery], media.Select(m => m.Role));
        Assert.All(media, m =>
        {
            Assert.StartsWith($"/media/packages/{package.Id:N}/", m.Path);
            Assert.Equal(TestPhotoSeeder.Marker, m.Caption);
            Assert.False(string.IsNullOrWhiteSpace(m.Alt));
            Assert.Equal((1600, 1067), (m.Width, m.Height));
        });
        Assert.Equal(4, FilesOnDisk().Length);
        Assert.Equal(4, media.Select(m => m.Path).Distinct().Count());
    }

    [Fact]
    public async Task A_poster_that_came_with_the_site_is_kept_as_the_poster()
    {
        var package = Add("giraffe-manor-grand-escape", _nairobi, Poster());

        await Seeder().SeedAsync(4, dryRun: false);

        var media = await _db.PackageMedia.Where(m => m.PackageId == package.Id).ToListAsync();
        Assert.Equal(MediaRole.Poster, media.Single(m => m.Path == "/images/packages/flyer.jpg").Role);
        Assert.Equal(1, media.Count(m => m.Role == MediaRole.Hero));
        Assert.StartsWith("/media/", media.Single(m => m.Role == MediaRole.Hero).Path);
        Assert.Equal(5, media.Count);
    }

    [Fact]
    public async Task Running_it_again_changes_nothing()
    {
        Add("sala-mara-escape", _mara);
        await Seeder().SeedAsync(4, dryRun: false);
        var before = FilesOnDisk().Length;
        _requested.Clear();

        var lines = await Seeder().SeedAsync(4, dryRun: false);

        Assert.Contains(lines, l => l.Contains("already has uploaded photos"));
        Assert.Equal(4, await _db.PackageMedia.CountAsync());
        Assert.Equal(before, FilesOnDisk().Length);
        Assert.Empty(_requested);
    }

    [Fact]
    public async Task A_dry_run_reports_what_it_would_do_and_changes_nothing()
    {
        Add("sala-mara-escape", _mara, Poster());

        var lines = await Seeder().SeedAsync(4, dryRun: true);

        Assert.Contains(lines, l => l.StartsWith("would") && l.Contains("sala-mara-escape") && l.Contains("4 photos"));
        Assert.Equal(1, await _db.PackageMedia.CountAsync());
        Assert.Equal(MediaRole.Hero, (await _db.PackageMedia.SingleAsync()).Role);
        Assert.Empty(FilesOnDisk());
        Assert.Empty(_requested);
    }

    [Fact]
    public async Task A_package_that_already_has_an_uploaded_photo_is_left_alone()
    {
        var mine = new PackageMedia { Id = Guid.NewGuid(), Role = MediaRole.Hero, Path = "/media/packages/x/mine.jpg", CreatedAt = DateTime.UtcNow };
        Add("uploaded-by-staff", _mara, mine);

        await Seeder().SeedAsync(4, dryRun: false);

        Assert.Equal(1, await _db.PackageMedia.CountAsync());
    }

    [Fact]
    public async Task Each_photo_is_downloaded_once_however_many_packages_use_it()
    {
        Add("one", _mara);
        Add("two", _mara);
        Add("three", _mara);

        await Seeder().SeedAsync(4, dryRun: false);

        Assert.Equal(_requested.Count, _requested.Distinct().Count());
        Assert.Equal(12, await _db.PackageMedia.CountAsync());
    }

    [Fact]
    public async Task Undo_removes_the_test_photos_and_files_and_restores_the_poster()
    {
        var kept = new PackageMedia { Id = Guid.NewGuid(), Role = MediaRole.Gallery, Path = "/media/packages/y/keep.jpg", SortKey = 9, CreatedAt = DateTime.UtcNow };
        var withPoster = Add("giraffe-manor-grand-escape", _nairobi, Poster());
        Add("sala-mara-escape", _mara);
        await Seeder().SeedAsync(4, dryRun: false);
        Assert.Equal(8, FilesOnDisk().Length);

        var lines = await Seeder().UndoAsync(dryRun: false);

        Assert.Equal(2, lines.Count);
        var left = await _db.PackageMedia.ToListAsync();
        var poster = Assert.Single(left);
        Assert.Equal(withPoster.Id, poster.PackageId);
        Assert.Equal(MediaRole.Hero, poster.Role); // back where it was
        Assert.Empty(FilesOnDisk());
        _ = kept;
    }

    [Fact]
    public async Task Undo_leaves_photos_staff_uploaded_alone()
    {
        var package = Add("mixed", _mara);
        await Seeder().SeedAsync(2, dryRun: false);
        _db.PackageMedia.Add(new PackageMedia
        {
            Id = Guid.NewGuid(), PackageId = package.Id, Role = MediaRole.Gallery, Path = "/media/packages/z/staff.jpg",
            Caption = "Sunset from the deck", SortKey = 50, CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await Seeder().UndoAsync(dryRun: false);

        Assert.Equal("Sunset from the deck", (await _db.PackageMedia.SingleAsync()).Caption);
    }

    [Fact]
    public async Task A_failed_download_leaves_that_package_exactly_as_it_was()
    {
        var package = Add("giraffe-manor-grand-escape", _nairobi, Poster());
        _failOnRequest = 3; // the third photo

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => Seeder().SeedAsync(4, dryRun: false));

        var media = await _db.PackageMedia.Where(m => m.PackageId == package.Id).ToListAsync();
        Assert.Equal(MediaRole.Hero, Assert.Single(media).Role);
        Assert.Empty(FilesOnDisk());
    }

    [Fact]
    public void Packages_start_at_different_photos_and_the_count_is_respected()
    {
        var a = TestPhotoSeeder.Pick("sala-mara-escape", "masai-mara", 4);
        var b = TestPhotoSeeder.Pick("salas-classic-safari", "masai-mara", 4);
        var unknown = TestPhotoSeeder.Pick("anything", "atlantis", 3);

        Assert.Equal(4, a.Count);
        Assert.Equal(4, a.Select(p => p.Url).Distinct().Count());
        Assert.NotEqual(a[0].Url, b[0].Url);
        Assert.Equal(3, unknown.Count);
        Assert.All(a, p => Assert.Contains("w=1600&h=1067", p.Url));
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
