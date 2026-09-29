using Arlink28.Api.Data.Entities.Common;

namespace Arlink28.Api.Data.Entities;

public class SeasonRange : BaseEntity
{
    public Guid SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
