using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.AdminPackages.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Arlink28.Api.Data.Seed;

/// <summary>
/// SAMPLE LISTINGS, so the flight, hotel and visa pages can be judged with something on them, the way the
/// holiday packages can. Published, with sample from-prices. Run with <c>dotnet run -- seed-products</c>
/// (see Program.cs); <c>seed-photos</c> afterwards adds photos to them like any other package.
///
/// - Create-only: a slug that already exists is left exactly as it is, so re-running never overwrites an edit.
/// - A destination or property the listing needs that is not in the database skips that listing.
/// - <c>--undo</c> deletes exactly these slugs (and nothing else), unless an enquiry is attached to one.
/// - The prices and fees are sample figures. Replace or remove them before real use.
/// </summary>
public class ProductSeeder(ApplicationDbContext db)
{
    public record Listing(
        ProductType Type,
        string Slug,
        string Title,
        string Destination,
        string Summary,
        long? FromPriceMinor,
        object Details,
        bool Featured = false,
        string[]? Included = null,
        string? Property = null);

    public static readonly IReadOnlyList<Listing> Listings =
    [
        // ── Flights: quoted on request, so these describe the route and what comes with it ──
        new(ProductType.Flight, "lagos-to-nairobi", "Lagos to Nairobi", "nairobi",
            "A return flight from Lagos to Nairobi, with a quote from our team for your dates.", 62000,
            new { origin = "Lagos", destination = "Nairobi", tripType = "Return", cabin = "Economy",
                  baggage = "Checked bag and hand luggage",
                  fareNotes = "Fares change with the season. Send us your dates and we reply with a price and the next steps." },
            Featured: true),
        new(ProductType.Flight, "lagos-to-accra", "Lagos to Accra", "accra",
            "A short return flight between Lagos and Accra.", null,
            new { origin = "Lagos", destination = "Accra", tripType = "Return", cabin = "Economy",
                  baggage = "Checked bag and hand luggage",
                  fareNotes = "Send us your dates and we reply with a price." }),
        new(ProductType.Flight, "nairobi-to-masai-mara", "Nairobi to Masai Mara", "masai-mara",
            "A one-way hop from Nairobi to the Mara airstrips, for the start of a safari.", 18500,
            new { origin = "Nairobi", destination = "Masai Mara", tripType = "OneWay", cabin = "Economy",
                  baggage = "A soft bag, as light aircraft require",
                  fareNotes = "Light-aircraft baggage limits are strict. We tell you the exact limit when we quote." }),
        new(ProductType.Flight, "lagos-to-cairo", "Lagos to Cairo", "cairo",
            "A return flight from Lagos to Cairo, in economy or business.", 74000,
            new { origin = "Lagos", destination = "Cairo", tripType = "Return", cabin = "Economy",
                  baggage = "Checked bag and hand luggage",
                  fareNotes = "Ask us about business class when you enquire." }),

        // ── Hotels: our own properties, so the name comes from the property ──
        new(ProductType.HotelReservation, "giraffe-manor-stay", "Giraffe Manor", "nairobi",
            "A stay at Giraffe Manor, where the giraffes join you at breakfast.", 95000,
            new { roomType = "Garden suite", boardBasis = "FullBoard",
                  cancellationTerms = "Cancellation terms are confirmed with your quote." },
            Featured: true,
            Included: ["Breakfast, lunch and dinner", "Giraffe feeding at the house", "Airport transfer on request"],
            Property: "giraffe-manor"),
        new(ProductType.HotelReservation, "salas-camp-stay", "Sala's Camp", "masai-mara",
            "A stay at Sala's Camp, with a private plunge pool and a view over the plains.", 78000,
            new { roomType = "Tented suite", boardBasis = "FullBoard",
                  cancellationTerms = "Cancellation terms are confirmed with your quote." },
            Included: ["All meals", "Private plunge pool", "En suite bathroom with a bath"],
            Property: "salas-camp"),
        new(ProductType.HotelReservation, "sasaab-stay", "Sasaab", "samburu",
            "A stay at Sasaab, a lodge in the Samburu hills.", 69000,
            new { roomType = "Suite", boardBasis = "FullBoard",
                  cancellationTerms = "Cancellation terms are confirmed with your quote." },
            Included: ["All meals", "Game drives", "A private deck"],
            Property: "sasaab"),

        // ── Visas: our service fee only; the government fee is left for the owner to confirm ──
        new(ProductType.VisaSupport, "kenya-tourist-visa-support", "Kenya tourist visa", "kenya",
            "We prepare and submit your Kenya tourist visa application.", 4500,
            new { country = "Kenya", visaType = "Tourist visa", currency = "USD", serviceFeeMinor = 4500,
                  requirements = new[] { "A passport valid for at least six months", "A recent passport photo", "Your travel dates and where you will stay" } },
            Featured: true),
        new(ProductType.VisaSupport, "ghana-tourist-visa-support", "Ghana tourist visa", "ghana",
            "We prepare and submit your Ghana tourist visa application.", 4500,
            new { country = "Ghana", visaType = "Tourist visa", currency = "USD", serviceFeeMinor = 4500,
                  requirements = new[] { "A passport valid for at least six months", "A recent passport photo", "Your travel dates and where you will stay" } }),
        new(ProductType.VisaSupport, "egypt-tourist-visa-support", "Egypt tourist visa", "egypt",
            "We prepare and submit your Egypt tourist visa application.", 4500,
            new { country = "Egypt", visaType = "Tourist visa", currency = "USD", serviceFeeMinor = 4500,
                  requirements = new[] { "A passport valid for at least six months", "A recent passport photo", "A return or onward ticket" } }),
        new(ProductType.VisaSupport, "zimbabwe-tourist-visa-support", "Zimbabwe visa for Victoria Falls", "zimbabwe",
            "We prepare your Zimbabwe visa for a Victoria Falls trip.", 4000,
            new { country = "Zimbabwe", visaType = "Tourist visa", currency = "USD", serviceFeeMinor = 4000,
                  requirements = new[] { "A passport valid for at least six months", "A recent passport photo", "Your travel dates" } }),
    ];

    private static string CategoryOf(ProductType type) => type switch
    {
        ProductType.Flight => "Flight",
        ProductType.HotelReservation => "HotelReservation",
        ProductType.VisaSupport => "VisaSupport",
        _ => type.ToString(),
    };

    public async Task<IReadOnlyList<string>> SeedAsync(bool dryRun, CancellationToken ct = default)
    {
        var lines = new List<string>();
        var now = DateTime.UtcNow;
        var order = 0;

        foreach (var l in Listings)
        {
            order++;
            if (await db.Packages.AnyAsync(p => p.Slug == l.Slug, ct))
            {
                lines.Add($"skip   {l.Slug}: already there");
                continue;
            }

            var destination = await db.Destinations.FirstOrDefaultAsync(d => d.Slug == l.Destination, ct);
            if (destination is null)
            {
                lines.Add($"skip   {l.Slug}: no destination '{l.Destination}'");
                continue;
            }

            var raw = JObject.FromObject(l.Details);
            if (l.Property is not null)
            {
                var property = await db.Properties.FirstOrDefaultAsync(p => p.Slug == l.Property, ct);
                if (property is null)
                {
                    lines.Add($"skip   {l.Slug}: no property '{l.Property}'");
                    continue;
                }
                raw["propertyId"] = property.Id;
            }

            // The same validation the admin applies, so a listing that would not save there fails here.
            var details = ProductDetails.Normalize(l.Type, raw);

            lines.Add($"{(dryRun ? "would " : "add   ")} {l.Type,-17} {l.Slug}");
            if (dryRun) continue;

            var package = new Package
            {
                Id = Guid.NewGuid(),
                Slug = l.Slug,
                ProductType = l.Type,
                Details = details,
                Status = PackageStatus.Published,
                Title = l.Title,
                Summary = l.Summary,
                Category = CategoryOf(l.Type),
                DestinationId = destination.Id,
                Adults = 1,
                BaseCurrency = "USD",
                FromPriceMinor = l.FromPriceMinor,
                Featured = l.Featured,
                SortOrder = order,
                PublishedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Packages.Add(package);

            var included = l.Included ?? [];
            for (var i = 0; i < included.Length; i++)
            {
                db.PackageFeatures.Add(new PackageFeature
                {
                    Id = Guid.NewGuid(),
                    PackageId = package.Id,
                    Section = FeatureSection.Included,
                    LabelOverride = included[i],
                    SortOrder = i,
                });
            }
            await db.SaveChangesAsync(ct);
        }
        return lines;
    }

    /// <summary>Deletes the listings above by slug. One with an enquiry attached is kept.</summary>
    public async Task<IReadOnlyList<string>> UndoAsync(bool dryRun, CancellationToken ct = default)
    {
        var lines = new List<string>();
        var slugs = Listings.Select(l => l.Slug).ToList();
        var packages = await db.Packages.Include(p => p.Features).Include(p => p.Media)
            .Where(p => slugs.Contains(p.Slug)).OrderBy(p => p.Slug).ToListAsync(ct);

        foreach (var package in packages)
        {
            if (await db.Enquiries.AnyAsync(e => e.PackageId == package.Id, ct))
            {
                lines.Add($"keep   {package.Slug}: has enquiries");
                continue;
            }
            lines.Add($"{(dryRun ? "would " : "remove")} {package.Slug}");
            if (dryRun) continue;
            db.PackageFeatures.RemoveRange(package.Features);
            db.PackageMedia.RemoveRange(package.Media);
            db.Packages.Remove(package);
            await db.SaveChangesAsync(ct);
        }
        return lines;
    }
}
