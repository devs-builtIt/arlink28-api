using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Catalogue.RequestModels;
using Arlink28.Api.Features.Catalogue.Services;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Tests;

public sealed class DestinationCatalogueTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly CatalogueService _service;
    private readonly Destination _botswana;
    private readonly Destination _chobe;
    private readonly Destination _okavango;

    public DestinationCatalogueTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        _botswana = Add("botswana", "Botswana", "BW", DestinationKind.Country, null, DestinationStatus.Published);
        _chobe = Add("chobe-national-park", "Chobe National Park", "BW", DestinationKind.Place, _botswana, DestinationStatus.Published);
        _okavango = Add("okavango-delta", "Okavango Delta", "BW", DestinationKind.Place, _botswana, DestinationStatus.Draft);
        Add("egypt", "Egypt", "EG", DestinationKind.Country, null, DestinationStatus.Draft);
        Add("nairobi", "Nairobi", "KE", DestinationKind.Place, null, DestinationStatus.Published);

        _chobe.Attractions =
        [
            new DestinationAttraction { Id = Guid.NewGuid(), Name = "River safari", SortOrder = 2 },
            new DestinationAttraction { Id = Guid.NewGuid(), Name = "Elephant crossing", SortOrder = 1 },
        ];
        _db.SaveChanges();
        _service = new CatalogueService(_db);
    }

    public void Dispose() => _db.Dispose();

    private Destination Add(string slug, string name, string country, DestinationKind kind, Destination? parent, DestinationStatus status)
    {
        var d = new Destination
        {
            Id = Guid.NewGuid(), Slug = slug, Name = name, Country = country, Kind = kind, Status = status, Parent = parent,
        };
        _db.Destinations.Add(d);
        return d;
    }

    [Fact]
    public async Task List_hides_drafts()
    {
        var list = await _service.ListDestinationsAsync();
        Assert.DoesNotContain(list, d => d.Slug is "egypt" or "okavango-delta");
        Assert.Contains(list, d => d.Slug == "botswana");
    }

    [Fact]
    public async Task List_filters_by_country_code_and_kind()
    {
        var places = await _service.ListDestinationsAsync("bw", "place");
        Assert.Equal(["chobe-national-park"], places.Select(d => d.Slug));
        Assert.Equal("botswana", places[0].ParentSlug);
        Assert.Equal("Place", places[0].Kind);
    }

    [Fact]
    public async Task List_ignores_an_unknown_kind()
    {
        var list = await _service.ListDestinationsAsync("BW", "nonsense");
        Assert.Equal(["botswana", "chobe-national-park"], list.Select(d => d.Slug).Order());
    }

    [Fact]
    public async Task Detail_returns_published_places_and_ordered_attractions()
    {
        var country = await _service.GetDestinationAsync("botswana");
        Assert.NotNull(country);
        Assert.Equal(["chobe-national-park"], country.Places.Select(p => p.Slug));

        var chobe = await _service.GetDestinationAsync("chobe-national-park");
        Assert.NotNull(chobe);
        Assert.Equal("botswana", chobe.Parent?.Slug);
        Assert.Equal(["Elephant crossing", "River safari"], chobe.Attractions.Select(a => a.Name));
    }

    [Fact]
    public async Task Detail_of_a_draft_is_null()
    {
        Assert.Null(await _service.GetDestinationAsync("okavango-delta"));
        Assert.Null(await _service.GetDestinationAsync("egypt"));
        Assert.Null(await _service.GetDestinationAsync("missing"));
    }

    [Fact]
    public async Task Detail_hides_a_draft_parent()
    {
        _chobe.Parent!.Status = DestinationStatus.Draft;
        await _db.SaveChangesAsync();
        var chobe = await _service.GetDestinationAsync("chobe-national-park");
        Assert.NotNull(chobe);
        Assert.Null(chobe.Parent);
    }

    [Fact]
    public async Task A_countrys_package_count_includes_its_places()
    {
        _db.Packages.Add(new Package
        {
            Id = Guid.NewGuid(), Slug = "chobe-safari", Title = "Chobe", Category = "SAFARI", BaseCurrency = "USD",
            DestinationId = _chobe.Id, Status = PackageStatus.Published, Nights = 3, MinNights = 3, Adults = 2,
        });
        await _db.SaveChangesAsync();
        Assert.Equal(1, (await _service.GetDestinationAsync("botswana"))!.PackageCount);
        Assert.Equal(1, (await _service.GetDestinationAsync("chobe-national-park"))!.PackageCount);
    }

    [Fact]
    public async Task The_package_filter_by_a_country_includes_packages_in_its_places()
    {
        _db.Packages.Add(new Package
        {
            Id = Guid.NewGuid(), Slug = "chobe-safari", Title = "Chobe", Category = "SAFARI", BaseCurrency = "USD",
            DestinationId = _chobe.Id, Status = PackageStatus.Published, Nights = 3, MinNights = 3, Adults = 2,
        });
        await _db.SaveChangesAsync();
        PackageListRequest For(string slug) => new(slug, null, null, null, null, null);

        Assert.Equal(["chobe-safari"], (await _service.ListPackagesAsync(For("botswana"))).Items.Select(i => i.Slug));
        Assert.Equal(["chobe-safari"], (await _service.ListPackagesAsync(For("chobe-national-park"))).Items.Select(i => i.Slug));
        Assert.Empty((await _service.ListPackagesAsync(For("egypt"))).Items);
    }
}
