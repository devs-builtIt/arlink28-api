using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Features.AdminPackages.RequestModels;

public record StayInput(Guid PropertyId, int Nights, string? RoomType);

/// <summary>Pick a shared feature with FeatureId, or write the package's own line in Label.</summary>
public record FeatureInput(FeatureSection Section, Guid? FeatureId, string? Label, string? Footnote);

/// <summary>Prices are in minor units (cents).</summary>
public record RateInput(Guid SeasonId, string Currency, long PriceMinor, long? ExtraNightPriceMinor);

/// <summary>Send an existing add-on's Id to keep it (quotes refer to it); leave Id out for a new one.</summary>
public record AddOnInput(Guid? Id, string Name, string? Description, AddOnUnit Unit, string Currency, long PriceMinor);
