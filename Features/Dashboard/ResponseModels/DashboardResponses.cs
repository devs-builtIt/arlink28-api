using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.Dashboard.ResponseModels;

/// <summary>One day in a series. The date is a UTC calendar day.</summary>
public record DayCount(DateOnly Date, int Count);

public record TypeCount(EnquiryType Type, int Count);

/// <summary>What the enquiries were about: a product type, or "General" for ones with no package.</summary>
public record ProductCount(string Product, int Count);

public record CurrencyTotal(string Currency, long TotalMinor, int Count);

public record PackageCount(Guid? PackageId, string Title, int Count);

/// <summary>An enquiry still waiting for a first reply.</summary>
public record AwaitingEnquiry(
    Guid Id, string Reference, string Name, EnquiryType Type, string? PackageTitle, DateTime CreatedAt);

public record DestinationCount(string Name, int Count);

/// <param name="Handled">Enquiries in the period that this person has acted on.</param>
/// <param name="Closed">Of those, how many are closed.</param>
public record StaffWorkload(Guid Id, string Username, int Handled, int Closed);

/// <summary>An enquiry with a check-in date still to come.</summary>
public record UpcomingTrip(
    Guid Id, string Reference, string Name, string? PackageTitle, DateOnly CheckIn, int? Nights, EnquiryStatus Status);

/// <param name="Total">Enquiries received inside the period.</param>
/// <param name="PreviousTotal">The same count for the period just before it, to compare against.</param>
/// <param name="New">Of those received in the period, how many are still new.</param>
/// <param name="Contacted">Of those received in the period, how many have been contacted.</param>
/// <param name="Closed">Of those received in the period, how many are closed.</param>
/// <param name="AwaitingAll">Every enquiry still waiting for a first reply, whenever it arrived.</param>
/// <param name="MedianHandleHours">Median hours from arrival to staff acting on it. Null when none were handled.</param>
/// <param name="Series">One entry per day of the period.</param>
/// <param name="PreviousSeries">The previous period, shifted to line up day by day with Series.</param>
/// <param name="ByWeekday">Enquiries by day of the week, Monday first.</param>
/// <param name="ByDestination">Where the enquiries about a listing were for, busiest first.</param>
/// <param name="Staff">Who has handled what, most first. Only staff who handled something.</param>
/// <param name="Upcoming">The next trips by check-in date, whatever period is shown.</param>
/// <param name="UpcomingNext30">How many enquiries have a check-in in the next 30 days.</param>
public record EnquiryMetrics(
    int Total,
    int PreviousTotal,
    int New,
    int Contacted,
    int Closed,
    int AwaitingAll,
    double? MedianHandleHours,
    IReadOnlyList<DayCount> Series,
    IReadOnlyList<DayCount> PreviousSeries,
    IReadOnlyList<TypeCount> ByType,
    IReadOnlyList<ProductCount> ByProduct,
    IReadOnlyList<int> ByWeekday,
    IReadOnlyList<CurrencyTotal> Quoted,
    IReadOnlyList<PackageCount> TopPackages,
    IReadOnlyList<AwaitingEnquiry> Awaiting,
    IReadOnlyList<DestinationCount> ByDestination,
    IReadOnlyList<StaffWorkload> Staff,
    IReadOnlyList<UpcomingTrip> Upcoming,
    int UpcomingNext30);

public record ProductStatusCount(string Product, int Published, int Draft, int Archived);

public record ListingToFix(Guid Id, string Title, string Product, string Reason);

/// <param name="NeedingAttention">Published listings that need something before they look finished.</param>
public record CatalogueMetrics(
    IReadOnlyList<ProductStatusCount> ByProduct,
    int Featured,
    int DestinationsPublished,
    int DestinationsDraft,
    int NeedingAttention,
    IReadOnlyList<ListingToFix> ToFix);

public record DashboardResponse(DateTime GeneratedAt, int Days, EnquiryMetrics Enquiries, CatalogueMetrics Catalogue);
