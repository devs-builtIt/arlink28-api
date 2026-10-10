using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Data.Seed;
using Arlink28.Api.Features.AdminPackages.Services;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Tests;

public sealed class ProductSeederTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly ProductSeeder _seeder;

    public ProductSeederTests() => _seeder = new ProductSeeder(_db);

    public void Dispose() => _db.Dispose();

    /// <summary>Every destination and property the sample listings name, so none is skipped.</summary>
    private void AddReferenceData()
    {
        var destinations = ProductSeeder.Listings.Select(l => l.Destination).Distinct()
            .Select(slug => new Destination { Id = Guid.NewGuid(), Slug = slug, Name = slug, Country = "KE", Status = DestinationStatus.Published })
            .ToList();
        _db.Destinations.AddRange(destinations);
        foreach (var slug in ProductSeeder.Listings.Where(l => l.Property is not null).Select(l => l.Property!).Distinct())
            _db.Properties.Add(new Property { Id = Guid.NewGuid(), Slug = slug, Name = slug, DestinationId = destinations[0].Id });
        _db.SaveChanges();
    }

    [Fact]
    public void The_listings_cover_every_service_and_have_unique_slugs()
    {
        var types = ProductSeeder.Listings.Select(l => l.Type).ToHashSet();
        Assert.Contains(ProductType.Flight, types);
        Assert.Contains(ProductType.HotelReservation, types);
        Assert.Contains(ProductType.VisaSupport, types);
        Assert.Equal(ProductSeeder.Listings.Count, ProductSeeder.Listings.Select(l => l.Slug).Distinct().Count());
    }

    [Fact]
    public async Task Adds_every_listing_published_with_details_the_admin_would_accept()
    {
        AddReferenceData();
        var lines = await _seeder.SeedAsync(dryRun: false);

        Assert.DoesNotContain(lines, l => l.StartsWith("skip"));
        var saved = await _db.Packages.Include(p => p.Features).ToListAsync();
        Assert.Equal(ProductSeeder.Listings.Count, saved.Count);
        Assert.All(saved, p =>
        {
            Assert.Equal(PackageStatus.Published, p.Status);
            Assert.NotNull(p.PublishedAt);
            Assert.False(string.IsNullOrWhiteSpace(p.Summary));
            Assert.NotNull(p.Details);
        });
        // The hotel's name comes from its property, so the details must point at it.
        Assert.All(saved.Where(p => p.ProductType == ProductType.HotelReservation),
            p => Assert.NotNull(ProductDetails.ToJson(p.Details)!["propertyId"]));
        Assert.All(saved.Where(p => p.ProductType == ProductType.HotelReservation), p => Assert.NotEmpty(p.Features));
    }

    [Fact]
    public async Task A_dry_run_changes_nothing()
    {
        AddReferenceData();
        var lines = await _seeder.SeedAsync(dryRun: true);

        Assert.Equal(ProductSeeder.Listings.Count, lines.Count(l => l.StartsWith("would")));
        Assert.Empty(_db.Packages);
    }

    [Fact]
    public async Task Running_twice_adds_nothing_and_never_overwrites_an_edit()
    {
        AddReferenceData();
        await _seeder.SeedAsync(false);
        var first = await _db.Packages.FirstAsync(p => p.Slug == "lagos-to-nairobi");
        first.Title = "Edited by the owner";
        await _db.SaveChangesAsync();

        var lines = await _seeder.SeedAsync(false);

        Assert.All(lines, l => Assert.StartsWith("skip", l));
        Assert.Equal(ProductSeeder.Listings.Count, await _db.Packages.CountAsync());
        Assert.Equal("Edited by the owner", (await _db.Packages.FirstAsync(p => p.Slug == "lagos-to-nairobi")).Title);
    }

    [Fact]
    public async Task A_listing_whose_destination_or_property_is_missing_is_skipped_not_failed()
    {
        // Only the destinations: the three hotels cannot find their properties.
        foreach (var slug in ProductSeeder.Listings.Select(l => l.Destination).Distinct())
            _db.Destinations.Add(new Destination { Id = Guid.NewGuid(), Slug = slug, Name = slug, Country = "KE", Status = DestinationStatus.Published });
        await _db.SaveChangesAsync();

        var lines = await _seeder.SeedAsync(false);

        Assert.Equal(3, lines.Count(l => l.StartsWith("skip") && l.Contains("no property")));
        Assert.DoesNotContain(await _db.Packages.ToListAsync(), p => p.ProductType == ProductType.HotelReservation);
        Assert.Contains(await _db.Packages.ToListAsync(), p => p.ProductType == ProductType.Flight);
    }

    [Fact]
    public async Task Undo_removes_the_listings_but_keeps_one_with_an_enquiry()
    {
        AddReferenceData();
        await _seeder.SeedAsync(false);
        var kept = await _db.Packages.FirstAsync(p => p.Slug == "lagos-to-accra");
        _db.Packages.Add(new Package { Id = Guid.NewGuid(), Slug = "owners-own", Title = "Not ours", Category = "Flight", ProductType = ProductType.Flight, DestinationId = kept.DestinationId });
        _db.Enquiries.Add(new Enquiry { Id = Guid.NewGuid(), PackageId = kept.Id, Name = "A guest", Email = "guest@example.com", Message = "Hello" });
        await _db.SaveChangesAsync();

        var lines = await _seeder.UndoAsync(false);

        Assert.Contains(lines, l => l.StartsWith("keep") && l.Contains("lagos-to-accra"));
        var left = await _db.Packages.Select(p => p.Slug).ToListAsync();
        Assert.Equal(["lagos-to-accra", "owners-own"], left.Order().ToArray());
    }
}
