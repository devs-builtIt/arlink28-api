using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

/// <summary>
/// A place the catalogue points at. Two levels: a country (no parent) and the places inside it.
/// Only Published destinations are visible publicly.
/// </summary>
public class Destination : BaseAuditableEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    public DestinationKind Kind { get; set; } = DestinationKind.Place;
    public DestinationStatus Status { get; set; } = DestinationStatus.Published;
    public Guid? ParentId { get; set; }
    public Destination? Parent { get; set; }

    public string? Tagline { get; set; }
    public string? Summary { get; set; }
    /// <summary>Markdown. Rendered and sanitised by the web app.</summary>
    public string? Description { get; set; }
    public string? BestTimeToVisit { get; set; }
    /// <summary>Public path from <c>IMediaStorage</c>.</summary>
    public string? HeroPath { get; set; }
    public string? HeroAlt { get; set; }
    /// <summary>Photographer and source, shown next to the hero where the licence asks for it.</summary>
    public string? HeroCredit { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int SortOrder { get; set; }
    /// <summary>First time it went live. Once set, the slug is locked so shared links never break.</summary>
    public DateTime? PublishedAt { get; set; }

    public ICollection<Destination> Places { get; set; } = [];
    public ICollection<DestinationAttraction> Attractions { get; set; } = [];
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Package> Packages { get; set; } = [];
}
