using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Dashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace Arlink28.Api.Tests;

public sealed class DashboardServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DashboardService _service;
    private readonly Guid _destinationId = Guid.NewGuid();
    private int _refs;

    public DashboardServiceTests() => _service = new DashboardService(_db);

    public void Dispose() => _db.Dispose();

    private static DateTime DaysAgo(int days, int hour = 10) =>
        DateTime.UtcNow.Date.AddDays(-days).AddHours(hour);

    private Enquiry AddEnquiry(
        DateTime at, EnquiryStatus status = EnquiryStatus.New, EnquiryType type = EnquiryType.General,
        Package? package = null, long? quoted = null, string? currency = null, TimeSpan? handledAfter = null,
        Staff? handler = null, DateOnly? checkIn = null)
    {
        var enquiry = new Enquiry
        {
            Id = Guid.NewGuid(),
            Reference = $"ENQ-{++_refs:0000}",
            Type = type,
            Status = status,
            Name = "A guest",
            Email = "guest@example.com",
            PackageId = package?.Id,
            PackageTitle = package?.Title,
            QuotedTotalMinor = quoted,
            Currency = currency,
            CreatedAt = at,
            UpdatedAt = handledAfter is null ? at : at + handledAfter.Value,
            HandledById = handler?.Id ?? (handledAfter is null ? null : Guid.NewGuid()),
            CheckIn = checkIn,
            Nights = checkIn is null ? null : 5,
        };
        _db.Enquiries.Add(enquiry);
        _db.SaveChanges();
        return enquiry;
    }

    private Package AddPackage(
        string title, ProductType type = ProductType.HolidayPackage, PackageStatus status = PackageStatus.Published,
        bool photo = true, bool price = true, bool summary = true, bool featured = false)
    {
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Slug = title.ToLowerInvariant().Replace(' ', '-'),
            Title = title,
            Category = type.ToString(),
            ProductType = type,
            Status = status,
            DestinationId = _destinationId,
            FromPriceMinor = price ? 100000 : null,
            Summary = summary ? "A summary." : null,
            Featured = featured,
        };
        if (photo)
            package.Media.Add(new PackageMedia { Id = Guid.NewGuid(), Role = MediaRole.Hero, Path = "/media/x.jpg" });
        _db.Packages.Add(package);
        _db.SaveChanges();
        return package;
    }

    [Fact]
    public async Task Days_are_kept_between_a_week_and_a_year()
    {
        Assert.Equal(7, (await _service.GetAsync(1)).Days);
        Assert.Equal(365, (await _service.GetAsync(9999)).Days);
        Assert.Equal(30, (await _service.GetAsync(30)).Days);
    }

    [Fact]
    public async Task An_empty_database_gives_zeros_not_errors()
    {
        var result = await _service.GetAsync(30);

        Assert.Equal(0, result.Enquiries.Total);
        Assert.Null(result.Enquiries.MedianHandleHours);
        Assert.Equal(30, result.Enquiries.Series.Count);
        Assert.All(result.Enquiries.Series, d => Assert.Equal(0, d.Count));
        Assert.Equal(7, result.Enquiries.ByWeekday.Count);
        Assert.Empty(result.Catalogue.ByProduct);
    }

    [Fact]
    public async Task Counts_this_period_and_the_one_before_it_apart()
    {
        AddEnquiry(DaysAgo(2));
        AddEnquiry(DaysAgo(5));
        AddEnquiry(DaysAgo(12));
        AddEnquiry(DaysAgo(40)); // the 30 days before: the 31st to 60th day back
        AddEnquiry(DaysAgo(100)); // older than both

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.PreviousTotal);
        Assert.Equal(3, result.Series.Sum(d => d.Count));
        Assert.Equal(1, result.PreviousSeries.Sum(d => d.Count));
    }

    [Fact]
    public async Task The_series_has_one_entry_per_day_ending_today_and_the_previous_one_lines_up_with_it()
    {
        AddEnquiry(DaysAgo(0));
        AddEnquiry(DaysAgo(0, 15));
        AddEnquiry(DaysAgo(59)); // the first day of the previous 30

        var result = (await _service.GetAsync(30)).Enquiries;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Equal(today, result.Series[^1].Date);
        Assert.Equal(2, result.Series[^1].Count);
        Assert.Equal(today.AddDays(-29), result.Series[0].Date);
        // The previous series is shifted forward, so index for index it covers the same day of the period.
        Assert.Equal(result.Series.Select(d => d.Date), result.PreviousSeries.Select(d => d.Date));
        Assert.Equal(1, result.PreviousSeries[0].Count);
    }

    [Fact]
    public async Task Splits_the_period_by_status_but_counts_everyone_still_waiting()
    {
        AddEnquiry(DaysAgo(1));
        AddEnquiry(DaysAgo(2), EnquiryStatus.Contacted, handledAfter: TimeSpan.FromHours(3));
        AddEnquiry(DaysAgo(3), EnquiryStatus.Closed, handledAfter: TimeSpan.FromHours(5));
        AddEnquiry(DaysAgo(90)); // still new, but long ago

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal((1, 1, 1), (result.New, result.Contacted, result.Closed));
        Assert.Equal(2, result.AwaitingAll);
    }

    [Fact]
    public async Task Awaiting_lists_the_oldest_unanswered_first()
    {
        var oldest = AddEnquiry(DaysAgo(20));
        AddEnquiry(DaysAgo(3));
        for (var i = 0; i < 6; i++) AddEnquiry(DaysAgo(1 + i));
        AddEnquiry(DaysAgo(25), EnquiryStatus.Closed, handledAfter: TimeSpan.FromHours(1));

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal(5, result.Awaiting.Count);
        Assert.Equal(oldest.Id, result.Awaiting[0].Id);
        Assert.Equal(result.Awaiting.OrderBy(a => a.CreatedAt).Select(a => a.Id), result.Awaiting.Select(a => a.Id));
    }

    [Fact]
    public async Task The_median_handle_time_ignores_enquiries_nobody_has_touched()
    {
        AddEnquiry(DaysAgo(1), EnquiryStatus.Contacted, handledAfter: TimeSpan.FromHours(2));
        AddEnquiry(DaysAgo(2), EnquiryStatus.Contacted, handledAfter: TimeSpan.FromHours(4));
        AddEnquiry(DaysAgo(3), EnquiryStatus.Closed, handledAfter: TimeSpan.FromHours(30));
        AddEnquiry(DaysAgo(4)); // new: not part of it

        Assert.Equal(4, (await _service.GetAsync(30)).Enquiries.MedianHandleHours);
    }

    [Fact]
    public async Task Groups_by_type_product_and_weekday()
    {
        var holiday = AddPackage("Safari", ProductType.HolidayPackage);
        var flight = AddPackage("Lagos to Nairobi", ProductType.Flight);
        AddEnquiry(DaysAgo(1), type: EnquiryType.Package, package: holiday);
        AddEnquiry(DaysAgo(2), type: EnquiryType.Package, package: holiday);
        AddEnquiry(DaysAgo(3), type: EnquiryType.Booking, package: flight);
        AddEnquiry(DaysAgo(4), type: EnquiryType.General);

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal(EnquiryType.Package, result.ByType[0].Type);
        Assert.Equal(2, result.ByType[0].Count);
        Assert.Equal(("HolidayPackage", 2), (result.ByProduct[0].Product, result.ByProduct[0].Count));
        Assert.Contains(result.ByProduct, p => p.Product == "General" && p.Count == 1);
        Assert.Equal(4, result.ByWeekday.Sum());
        Assert.Equal(7, result.ByWeekday.Count);
    }

    private Staff AddStaff(string username)
    {
        var staff = new Staff { Id = Guid.NewGuid(), Username = username, Email = $"{username}@example.com" };
        _db.Staff.Add(staff);
        _db.SaveChanges();
        return staff;
    }

    [Fact]
    public async Task Ranks_destinations_by_the_enquiries_about_their_listings()
    {
        var kenya = new Destination { Id = Guid.NewGuid(), Slug = "kenya", Name = "Kenya", Country = "KE" };
        var ghana = new Destination { Id = Guid.NewGuid(), Slug = "ghana", Name = "Ghana", Country = "GH" };
        _db.Destinations.AddRange(kenya, ghana);
        await _db.SaveChangesAsync();
        var safari = AddPackage("Safari");
        safari.DestinationId = kenya.Id;
        var beach = AddPackage("Beach");
        beach.DestinationId = ghana.Id;
        await _db.SaveChangesAsync();
        AddEnquiry(DaysAgo(1), type: EnquiryType.Package, package: safari);
        AddEnquiry(DaysAgo(2), type: EnquiryType.Package, package: safari);
        AddEnquiry(DaysAgo(3), type: EnquiryType.Package, package: beach);
        AddEnquiry(DaysAgo(4)); // general: no destination

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal(("Kenya", 2), (result.ByDestination[0].Name, result.ByDestination[0].Count));
        Assert.Equal(("Ghana", 1), (result.ByDestination[1].Name, result.ByDestination[1].Count));
        Assert.Equal(2, result.ByDestination.Count);
    }

    [Fact]
    public async Task Shows_who_handled_what_and_ignores_enquiries_nobody_acted_on()
    {
        var anna = AddStaff("anna");
        var kwame = AddStaff("kwame");
        AddEnquiry(DaysAgo(1), EnquiryStatus.Closed, handledAfter: TimeSpan.FromHours(2), handler: anna);
        AddEnquiry(DaysAgo(2), EnquiryStatus.Contacted, handledAfter: TimeSpan.FromHours(2), handler: anna);
        AddEnquiry(DaysAgo(3), EnquiryStatus.Contacted, handledAfter: TimeSpan.FromHours(2), handler: kwame);
        AddEnquiry(DaysAgo(4)); // new, nobody has touched it

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal(2, result.Staff.Count);
        Assert.Equal(("anna", 2, 1), (result.Staff[0].Username, result.Staff[0].Handled, result.Staff[0].Closed));
        Assert.Equal(("kwame", 1, 0), (result.Staff[1].Username, result.Staff[1].Handled, result.Staff[1].Closed));
    }

    [Fact]
    public async Task Lists_the_next_trips_by_check_in_and_counts_those_in_the_next_thirty_days()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        AddEnquiry(DaysAgo(1), checkIn: today.AddDays(40));
        var soonest = AddEnquiry(DaysAgo(2), checkIn: today.AddDays(5));
        AddEnquiry(DaysAgo(3), checkIn: today.AddDays(20));
        AddEnquiry(DaysAgo(4), checkIn: today.AddDays(-3)); // already gone
        AddEnquiry(DaysAgo(5), EnquiryStatus.Closed, handledAfter: TimeSpan.FromHours(1), checkIn: today.AddDays(2)); // closed
        AddEnquiry(DaysAgo(6)); // no date

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal(3, result.Upcoming.Count);
        Assert.Equal(soonest.Id, result.Upcoming[0].Id);
        Assert.Equal(result.Upcoming.OrderBy(t => t.CheckIn).Select(t => t.Id), result.Upcoming.Select(t => t.Id));
        Assert.Equal(2, result.UpcomingNext30);
    }

    [Fact]
    public async Task Totals_quotes_by_currency_and_ranks_the_most_asked_about_packages()
    {
        var safari = AddPackage("Safari");
        var beach = AddPackage("Beach");
        AddEnquiry(DaysAgo(1), type: EnquiryType.Package, package: safari, quoted: 250000, currency: "USD");
        AddEnquiry(DaysAgo(2), type: EnquiryType.Package, package: safari, quoted: 150000, currency: "USD");
        AddEnquiry(DaysAgo(3), type: EnquiryType.Package, package: beach, quoted: 90000, currency: "GBP");
        AddEnquiry(DaysAgo(4), type: EnquiryType.Package, package: beach); // no quote

        var result = (await _service.GetAsync(30)).Enquiries;

        Assert.Equal(("USD", 400000L, 2), (result.Quoted[0].Currency, result.Quoted[0].TotalMinor, result.Quoted[0].Count));
        Assert.Equal(("GBP", 90000L, 1), (result.Quoted[1].Currency, result.Quoted[1].TotalMinor, result.Quoted[1].Count));
        // Level on two each, so the title breaks the tie.
        Assert.Equal(("Beach", 2), (result.TopPackages[0].Title, result.TopPackages[0].Count));
        Assert.Equal(("Safari", 2), (result.TopPackages[1].Title, result.TopPackages[1].Count));
    }

    [Fact]
    public async Task The_catalogue_counts_each_product_by_status_and_flags_unfinished_listings()
    {
        AddPackage("Done", featured: true);
        AddPackage("No photo", photo: false);
        AddPackage("No summary", summary: false);
        AddPackage("No price", price: false);
        AddPackage("Draft one", status: PackageStatus.Draft, photo: false);
        AddPackage("A flight", ProductType.Flight);
        _db.Destinations.Add(new Destination { Id = _destinationId, Slug = "kenya", Name = "Kenya", Country = "KE", Status = DestinationStatus.Published });
        _db.Destinations.Add(new Destination { Id = Guid.NewGuid(), Slug = "mali", Name = "Mali", Country = "ML", Status = DestinationStatus.Draft });
        await _db.SaveChangesAsync();

        var result = (await _service.GetAsync(30)).Catalogue;

        var holidays = result.ByProduct.Single(p => p.Product == "HolidayPackage");
        Assert.Equal((4, 1, 0), (holidays.Published, holidays.Draft, holidays.Archived));
        Assert.Equal(1, result.Featured);
        Assert.Equal((1, 1), (result.DestinationsPublished, result.DestinationsDraft));
        Assert.Equal(3, result.NeedingAttention);
        Assert.Equal(["No photo", "No price", "No summary"], result.ToFix.Select(f => f.Reason).Order().ToArray());
        Assert.DoesNotContain(result.ToFix, f => f.Title == "Draft one"); // drafts are not live, so nothing to fix yet
    }
}
