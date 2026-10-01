using Arlink28.Api.Data.Entities;

namespace Arlink28.Api.Data.Seed;

// Initial package catalogue, transcribed from the 17 partner posters in
// "Company docs/Packages/" (2026-09-25). 17 posters → 13 packages: posters for
// the same stay in different seasons are one package with one rate per season.
//
// Ported from the TypeScript seed (arlink28-nextjs, commit a925948,
// packages/db/src/seed/catalogue-data.ts). Packages whose posters contradict
// each other are DRAFT with a DataIssue; they stay off the public API until
// someone confirms the numbers and publishes them.

public record SeedDestination(string Slug, string Name, string Country);
public record SeedPartner(string Slug, string Name, string? Tagline);
public record SeedProperty(string Slug, string Name, string Partner, string Destination);
public record SeedSeason(string Slug, string Name, string Partner, (string Start, string End)[] Ranges);
public record SeedFeature(string Slug, string Label, string Icon);

/// <summary>A library feature (Key) with optional per-package wording, or free text (Text).</summary>
public record FeatureRef(string? Key = null, string? Label = null, string? Footnote = null, string? Text = null);

public record SeedStay(string Property, int Nights, string? RoomType = null);
public record SeedRate(string Season, long PriceMinor);
public record SeedAddOn(string Name, string? Description, AddOnUnit Unit, long PriceMinor);

public record SeedPackage(
    string Slug,
    PackageStatus Status,
    string Title,
    string Subtitle,
    string Summary,
    string Category,
    string Destination,
    int Nights,
    int MinNights,
    int Adults,
    int Children,
    int SortOrder,
    SeedStay[] Stays,
    Dictionary<FeatureSection, FeatureRef[]> Features,
    SeedRate[] Rates,
    SeedAddOn[] AddOns,
    string[] Sources,
    string? HeroImage = null,
    string? DataIssue = null);

public static class CatalogueSeedData
{
    public const string BaseCurrency = "USD";

    public static readonly SeedDestination[] Destinations =
    [
        new("nairobi", "Nairobi", "KE"),
        new("masai-mara", "Masai Mara", "KE"),
        new("samburu", "Samburu", "KE"),
        new("accra", "Accra", "GH"),
    ];

    public static readonly SeedPartner[] Partners =
    [
        new("giraffe-manor", "Giraffe Manor", "An exclusive partnership. Extraordinary experiences."),
        new("the-safari-collection", "The Safari Collection", null),
    ];

    public static readonly SeedProperty[] Properties =
    [
        new("giraffe-manor", "Giraffe Manor", "giraffe-manor", "nairobi"),
        new("salas-camp", "Sala's Camp", "the-safari-collection", "masai-mara"),
        new("sasaab", "Sasaab", "the-safari-collection", "samburu"),
    ];

    private const string Savings = "safari-collection-savings-2026";
    private const string Peak = "safari-collection-peak-2026";
    private const string Gm2026 = "giraffe-manor-2026";

    public static readonly SeedSeason[] Seasons =
    [
        new(Savings, "Savings Season 2026", "the-safari-collection",
            [("2026-01-06", "2026-05-31"), ("2026-11-01", "2026-12-15")]),
        new(Peak, "Peak Season 2026", "the-safari-collection",
            [("2026-01-01", "2026-01-05"), ("2026-06-01", "2026-10-31"), ("2026-12-16", "2026-12-31")]),
        // The Giraffe Manor posters carry no dates. Assumed valid for check-ins in
        // calendar 2026 until the partner confirms (see docs/packages-api-plan.md).
        new(Gm2026, "2026", "giraffe-manor", [("2026-01-01", "2026-12-31")]),
    ];

    /// <summary>Reusable inclusion/exclusion items; icons are Font Awesome 6 (free) names.</summary>
    public static readonly SeedFeature[] Features =
    [
        // stay
        new("luxury-tent-plunge-pool", "Luxury tent with private plunge pool", "bed"),
        new("nights-at-property", "Nights at the property", "bed"),
        new("luxury-accommodation", "Luxury accommodation", "house"),
        // food & drink
        new("all-meals", "All meals", "utensils"),
        new("house-wines", "House wines", "wine-glass"),
        new("house-soft-drinks", "House soft drinks", "glass-water"),
        new("house-beers", "House beers", "beer-mug-empty"),
        new("house-spirits", "House spirits", "martini-glass"),
        new("house-drinks", "House wines, soft drinks, beers & spirits", "wine-glass"),
        new("bush-meals", "Bush meals", "bell-concierge"),
        new("sundowners", "Sundowners", "sun"),
        new("orchid-house-dining", "Orchid House dining", "utensils"),
        // activities
        new("game-drives", "Game drives", "car-side"),
        new("nature-walks", "Nature walks", "person-hiking"),
        new("bush-volleyball", "Seasonal bush volleyball", "volleyball"),
        new("childrens-activities", "Children's activities", "children"),
        new("camp-activities", "Other camp activities", "binoculars"),
        new("afew-giraffe-centre", "AFEW Giraffe Centre entry", "ticket"),
        new("boules-croquet", "Boules & croquet", "baseball-bat-ball"),
        new("retreat-access", "Access to The Retreat during check-in & check-out", "spa"),
        new("park-fees", "Park fees", "ticket"),
        // services
        new("laundry", "Laundry", "shirt"),
        new("wifi", "Wi-Fi", "wifi"),
        new("vat", "VAT", "receipt"),
        // transfers
        new("keekorok-transfers", "Transfers from Keekorok Airstrip", "plane-arrival"),
        new("camp-airstrip-transfers", "Camp and airstrip transfers", "plane-arrival"),
        new("airport-transfers", "Airport transfers", "plane-arrival"),
        new("karen-langata-transfers", "Arrival & departure transfers – Karen & Langata area", "van-shuttle"),
        // ARLink28 premium services
        new("vip-home-to-airport", "VIP transfer from home to airport", "car"),
        new("vip-airport-to-resort", "VIP transfer from airport to resort", "plane"),
        new("meet-and-greet", "Meet & greet service from the airport to the destination", "people-group"),
        // accommodation highlights
        new("private-plunge-pools", "All tents have private plunge pools", "water-ladder"),
        new("ensuite-bath", "En suite bathrooms with a bath", "bath"),
        new("flushing-toilet", "Flushing toilet", "toilet"),
        new("plumbed-shower", "Plumbed shower", "shower"),
        // exclusions
        new("champagne", "Champagne", "champagne-glasses"),
        new("luxury-spirits", "Luxury spirits", "wine-bottle"),
        new("selected-wines", "Selected wines", "wine-glass-empty"),
        new("cigars", "Cigars", "smoking"),
        new("tips", "Tips", "hand-holding-dollar"),
        new("evacuation-insurance", "Travel emergency evacuation insurance", "kit-medical"),
        new("health-insurance", "Health insurance", "notes-medical"),
        new("massage-treatments", "Massage treatments", "spa"),
        new("personal-effects", "Personal effects", "suitcase"),
    ];

    // ---- shared bundles (the posters repeat these verbatim) --------------------

    private static FeatureRef K(string key, string? label = null, string? footnote = null) =>
        new(Key: key, Label: label, Footnote: footnote);

    private static FeatureRef T(string text) => new(Text: text);

    private static readonly FeatureRef[] SafariDrinksAndMeals =
        [K("all-meals"), K("house-wines"), K("house-soft-drinks"), K("house-beers"), K("house-spirits")];
    private static readonly FeatureRef[] SafariActivities = [K("game-drives"), K("bush-meals"), K("sundowners")];
    private static readonly FeatureRef[] SafariExtras =
        [K("laundry"), K("bush-volleyball"), K("nature-walks"), K("childrens-activities"), K("camp-activities")];

    private static FeatureRef[] SalaIncluded(int nights) =>
    [
        K("luxury-tent-plunge-pool", $"{nights} nights in a luxury tent with private plunge pool"),
        .. SafariDrinksAndMeals,
        .. SafariActivities,
        K("keekorok-transfers"),
        .. SafariExtras,
    ];

    private static FeatureRef[] MultiCampIncluded(params (string Name, int Nights)[] stays) =>
    [
        .. stays.Select(s => K("nights-at-property", $"{s.Nights} nights in {s.Name}")),
        K("luxury-accommodation"),
        .. SafariDrinksAndMeals,
        .. SafariActivities,
        K("camp-airstrip-transfers"),
        .. SafariExtras,
    ];

    private static readonly FeatureRef[] SafariPremium =
        [K("vip-home-to-airport"), K("vip-airport-to-resort"), K("meet-and-greet")];
    private static readonly FeatureRef[] SalaHighlights =
        [K("private-plunge-pools"), K("ensuite-bath"), K("flushing-toilet"), K("plumbed-shower")];
    private static readonly FeatureRef[] MultiHighlights =
        [K("private-plunge-pools", "Private plunge pools"), K("ensuite-bath"), K("flushing-toilet"), K("plumbed-shower")];
    private static readonly FeatureRef[] SafariExcluded =
    [
        .. new[] { "champagne", "luxury-spirits", "selected-wines", "cigars", "tips", "evacuation-insurance", "personal-effects" }
            .Select(k => K(k)),
    ];

    private static FeatureRef[] SalaVehicle(string packageName, string party) =>
    [
        T($"For {party}: {packageName} guests staying in the standard Keekorok Tent with Pool or Forest Tent with Pool receive shared game-drive vehicle use."),
        T("Private exclusive vehicle use is available at an extra charge of $490 per day."),
    ];

    private static FeatureRef[] MultiVehicle(string packageName) =>
    [
        T($"For two adults, {packageName} guests receive shared game-drive vehicle use."),
        T("Private exclusive vehicle use is available at an extra charge of $490 per day."),
    ];

    private static readonly FeatureRef[] RetreatPerk =
    [
        T("Eligibility for a complimentary retreat day pass and 30% off the retreat day room or early bed & breakfast package."),
    ];

    private static readonly SeedAddOn PrivateVehicle = new(
        "Private exclusive vehicle",
        "Exclusive use of a game-drive vehicle for your party, charged per day of your stay.",
        AddOnUnit.PerDay,
        49_000);

    private static readonly FeatureRef[] GmIncluded =
    [
        K("all-meals"),
        K("house-drinks"),
        K("laundry", "Laundry service"),
        K("wifi"),
        K("afew-giraffe-centre"),
        K("orchid-house-dining"),
        K("retreat-access"),
        K("boules-croquet"),
        K("vat"),
        K("karen-langata-transfers", null, "Subject to applicable collection and departure times."),
    ];
    private static readonly FeatureRef[] GmPremium =
    [
        K("vip-home-to-airport", "VIP transfer from home to airport", "If required"),
        K("meet-and-greet", "Meet & greet service at the airport"),
        K("vip-airport-to-resort"),
    ];
    private static readonly FeatureRef[] GmExcluded =
    [
        .. new[] { "champagne", "luxury-spirits", "selected-wines", "tips", "health-insurance", "massage-treatments", "personal-effects" }
            .Select(k => K(k)),
    ];

    private const string SalaSummary =
        "Luxury tent stay with curated safari inclusions and added premium ARLink28 travel services.";
    private const string MultiSummary =
        "Luxury multi-destination travel with curated safari inclusions and added premium ARLink28 travel services.";
    private const string SalaRoom = "Keekorok Tent with Pool or Forest Tent with Pool";
    private const string GmElegance =
        "Step into elegance, where heritage, wildlife and warm hospitality create unforgettable moments.";
    private const string GmFamily =
        "Create unforgettable family memories with extraordinary experiences at Giraffe Manor.";

    private static Dictionary<FeatureSection, FeatureRef[]> GmFeatures() => new()
    {
        [FeatureSection.Included] = GmIncluded,
        [FeatureSection.PremiumService] = GmPremium,
        [FeatureSection.Excluded] = GmExcluded,
    };

    private static Dictionary<FeatureSection, FeatureRef[]> SalaFeatures(int nights, string name, string party) => new()
    {
        [FeatureSection.Included] = SalaIncluded(nights),
        [FeatureSection.PremiumService] = SafariPremium,
        [FeatureSection.Highlight] = SalaHighlights,
        [FeatureSection.Vehicle] = SalaVehicle(name, party),
        [FeatureSection.Excluded] = SafariExcluded,
    };

    // Hero photos live in arlink28-nextjs at apps/web/public/images/packages/.
    private static string Hero(string file) => $"/images/packages/{file}";

    public static readonly SeedPackage[] Packages =
    [
        // ---- The Safari Collection: Sala's Camp, Masai Mara ---------------------
        new("sala-mara-escape", PackageStatus.Published, "Sala Mara Escape",
            "2-Night Fully Inclusive Luxury Masai Mara Safari Experience for Two Adults", SalaSummary,
            "SAFARI", "masai-mara", 2, 2, 2, 0, 10,
            [new("salas-camp", 2, SalaRoom)],
            SalaFeatures(2, "Sala Mara Escape", "two adults"),
            [new(Savings, 747_200)],
            [PrivateVehicle],
            ["WhatsApp Image 2026-09-25 at 02.19.33.jpeg"]),

        new("salas-classic-safari", PackageStatus.Published, "Sala's Classic Safari",
            "3-Night Fully Inclusive Luxury Masai Mara Safari Experience for Two Adults", SalaSummary,
            "SAFARI", "masai-mara", 3, 3, 2, 0, 20,
            [new("salas-camp", 3, SalaRoom)],
            SalaFeatures(3, "Sala's Classic Safari", "two adults"),
            [new(Peak, 1_775_000)],
            [PrivateVehicle],
            ["WhatsApp Image 2026-09-25 at 02.19.33 (1).jpeg"]),

        new("salas-extended-mara-experience", PackageStatus.Draft, "Sala's Extended Mara Experience",
            "4-Night Fully Inclusive Luxury Masai Mara Safari Experience for Two Adults", SalaSummary,
            "SAFARI", "masai-mara", 4, 4, 2, 0, 30,
            [new("salas-camp", 4, SalaRoom)],
            SalaFeatures(4, "Sala's Extended Mara Experience", "two adults"),
            [new(Savings, 1_494_400), new(Peak, 2_366_600)],
            [PrivateVehicle],
            ["WhatsApp Image 2026-09-25 at 02.19.33 (2).jpeg", "WhatsApp Image 2026-09-25 at 02.19.33 (3).jpeg"],
            DataIssue: "Both posters say 'Savings Season' with the same dates but show $14,944 and $23,666. $23,666 matches the peak " +
                       "per-night rate of Sala's Classic Safari ($17,750 / 3 × 4), so it is seeded as the peak rate. Confirm with the partner."),

        new("salas-family-safari", PackageStatus.Draft, "Sala's Family Safari",
            "4-Night Fully Inclusive Luxury Masai Mara Safari Experience for Two Adults & Two Children", SalaSummary,
            "SAFARI", "masai-mara", 4, 4, 2, 2, 40,
            [new("salas-camp", 4, SalaRoom)],
            SalaFeatures(4, "Sala's Family Safari", "two adults and two children"),
            [new(Savings, 2_688_400), new(Peak, 3_788_200)],
            [PrivateVehicle],
            ["WhatsApp Image 2026-09-25 at 02.19.33 (4).jpeg", "WhatsApp Image 2026-09-25 at 02.19.34 (4).jpeg"],
            DataIssue: "Both posters show the PEAK season dates but different prices ($37,882 and $26,884). The lower price is seeded as " +
                       "the savings rate by analogy with the other two-season packages, which is a guess. Confirm with the partner."),

        // ---- The Safari Collection: multi-camp ----------------------------------
        new("safari-collection-explorer", PackageStatus.Draft, "Safari Collection Explorer",
            "7-Night Luxury Multi-Destination Safari for Two Adults", MultiSummary,
            "SAFARI", "masai-mara", 7, 7, 2, 0, 50,
            [new("salas-camp", 4), new("sasaab", 3)],
            new()
            {
                [FeatureSection.Included] = MultiCampIncluded(("Sala's Camp", 4), ("Sasaab", 3)),
                [FeatureSection.PremiumService] = SafariPremium,
                [FeatureSection.Highlight] = MultiHighlights,
                [FeatureSection.Vehicle] = MultiVehicle("Safari Collection Explorer"),
                [FeatureSection.Perk] = RetreatPerk,
                [FeatureSection.Excluded] = SafariExcluded,
            },
            [new(Savings, 1_936_900), new(Peak, 3_398_500)],
            [PrivateVehicle],
            ["WhatsApp Image 2026-09-25 at 02.19.34 (2).jpeg", "WhatsApp Image 2026-09-25 at 02.19.34 (3).jpeg"],
            DataIssue: "The savings poster ($19,369) is 4 nights Sasaab + 3 nights Sala's Camp; the peak poster ($33,985) is 4 nights " +
                       "Sala's Camp + 3 nights Sasaab. One package has one itinerary: seeded with the peak split. Confirm whether these " +
                       "are two different products."),

        new("ultimate-safari-collection-journey", PackageStatus.Published, "Ultimate Safari Collection Journey",
            "10-Night Premium Long-Stay Safari for Two Adults", MultiSummary,
            "SAFARI", "masai-mara", 10, 10, 2, 0, 60,
            [new("salas-camp", 5), new("sasaab", 5)],
            new()
            {
                [FeatureSection.Included] = MultiCampIncluded(("Sala's Camp", 5), ("Sasaab", 5)),
                [FeatureSection.PremiumService] = SafariPremium,
                [FeatureSection.Highlight] = MultiHighlights,
                [FeatureSection.Vehicle] = MultiVehicle("Ultimate Safari Collection Journey"),
                [FeatureSection.Perk] = RetreatPerk,
                [FeatureSection.Excluded] = SafariExcluded,
            },
            [new(Savings, 3_088_900), new(Peak, 4_484_800)],
            [PrivateVehicle],
            ["WhatsApp Image 2026-09-25 at 02.19.34.jpeg", "WhatsApp Image 2026-09-25 at 02.19.34 (1).jpeg"]),

        // ---- Giraffe Manor, Nairobi ---------------------------------------------
        new("giraffe-manor-signature-escape", PackageStatus.Published, "Giraffe Manor Signature Escape",
            "2 Nights / 3 Days for Two Adults", GmElegance,
            "LODGE", "nairobi", 2, 2, 2, 0, 110,
            [new("giraffe-manor", 2)], GmFeatures(),
            [new(Gm2026, 589_600)], [],
            ["WhatsApp Image 2026-09-25 at 02.19.35 (3).jpeg"],
            HeroImage: Hero("giraffe-manor-signature-escape.jpg")),

        new("giraffe-manor-grand-escape", PackageStatus.Published, "Giraffe Manor Grand Escape",
            "3 Nights / 4 Days for Two Adults", GmElegance,
            "LODGE", "nairobi", 3, 3, 2, 0, 120,
            [new("giraffe-manor", 3)], GmFeatures(),
            [new(Gm2026, 848_800)], [],
            ["WhatsApp Image 2026-09-25 at 02.19.35 (2).jpeg"],
            HeroImage: Hero("giraffe-manor-grand-escape.jpg")),

        new("giraffe-manor-luxury-escape", PackageStatus.Published, "Giraffe Manor Luxury Escape",
            "4 Nights / 5 Days for Two Adults",
            "Indulge in sophisticated comfort, exceptional service and extraordinary moments at Giraffe Manor.",
            "LODGE", "nairobi", 4, 4, 2, 0, 130,
            [new("giraffe-manor", 4)], GmFeatures(),
            [new(Gm2026, 1_136_800)], [],
            ["WhatsApp Image 2026-09-25 at 02.19.35.jpeg"],
            HeroImage: Hero("giraffe-manor-luxury-escape.jpg")),

        new("giraffe-manor-family-experience", PackageStatus.Published, "Giraffe Manor Family Experience",
            "2 Nights / 3 Days for Two Adults + Two Children",
            "Create timeless moments together, surrounded by elegance, heritage and the gentle giants of Giraffe Manor.",
            "LODGE", "nairobi", 2, 2, 2, 2, 140,
            [new("giraffe-manor", 2)], GmFeatures(),
            [new(Gm2026, 968_500)], [],
            ["WhatsApp Image 2026-09-25 at 02.19.35 (1).jpeg"],
            HeroImage: Hero("giraffe-manor-family-experience.jpg")),

        new("giraffe-manor-family-celebration", PackageStatus.Published, "Giraffe Manor Family Celebration",
            "3 Nights / 4 Days for Two Adults + Two Children", GmFamily,
            "LODGE", "nairobi", 3, 3, 2, 2, 150,
            [new("giraffe-manor", 3)], GmFeatures(),
            [new(Gm2026, 1_459_900)], [],
            ["WhatsApp Image 2026-09-25 at 02.19.36.jpeg"],
            HeroImage: Hero("giraffe-manor-family-celebration.jpg")),

        new("giraffe-manor-family-celebration-3-children", PackageStatus.Published, "Giraffe Manor Family Celebration (3 Children)",
            "2 Nights / 3 Days for Two Adults + Three Children", GmFamily,
            "LODGE", "nairobi", 2, 2, 2, 3, 160,
            [new("giraffe-manor", 2)], GmFeatures(),
            [new(Gm2026, 1_180_900)], [],
            ["WhatsApp Image 2026-09-25 at 02.19.36 (1).jpeg"],
            HeroImage: Hero("giraffe-manor-family-celebration-3.jpg")),

        new("giraffe-and-nairobi-wildlife-escape", PackageStatus.Published, "Giraffe and Nairobi Wildlife Escape",
            "2 Nights / 3 Days for Two Adults", "Experience the best of Nairobi's wildlife and iconic landscapes.",
            "SAFARI", "nairobi", 2, 2, 2, 0, 170,
            [new("giraffe-manor", 2)],
            new()
            {
                [FeatureSection.Included] =
                [
                    K("all-meals"), K("house-drinks"), K("laundry", "Laundry service"), K("wifi"),
                    K("game-drives"), K("park-fees"), K("airport-transfers"),
                ],
                [FeatureSection.PremiumService] =
                [
                    K("vip-home-to-airport", "VIP transfer from home to airport", "If required"),
                    K("meet-and-greet", "Meet & greet service at the airport"),
                    K("vip-airport-to-resort"),
                ],
                [FeatureSection.Excluded] = GmExcluded,
                // Verbatim from the poster, although Included also lists "Park fees" — flagged for the owner.
                [FeatureSection.Note] = [T("Nairobi National Park fees are not included.")],
            },
            [new(Gm2026, 658_900)], [],
            ["WhatsApp Image 2026-09-25 at 02.19.37.jpeg"],
            HeroImage: Hero("giraffe-nairobi-wildlife-escape.jpg")),
    ];
}
