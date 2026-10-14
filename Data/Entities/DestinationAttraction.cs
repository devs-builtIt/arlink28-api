using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

/// <summary>A thing to see or do at a destination. Edited as an ordered list.</summary>
public class DestinationAttraction : BaseAuditableEntity
{
    public Guid DestinationId { get; set; }
    public Destination Destination { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? PhotoPath { get; set; }
    public string? PhotoAlt { get; set; }
    public string? PhotoCredit { get; set; }
    public int SortOrder { get; set; }
}
