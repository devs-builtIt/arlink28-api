using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Catalogue.Services;
using Arlink28.Api.Features.Enquiries.RequestModels;
using Arlink28.Api.Features.Enquiries.Services;
using Arlink28.Api.Features.Enquiries.Services.Interfaces;
using Arlink28.Api.Features.Shared.Services;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers;
using Arlink28.Api.Helpers.Settings;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Tests;

public sealed class EnquiryServiceTests : IDisposable
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly ApplicationDbContext _db;
    private readonly EnquiryService _service;
    private readonly Guid _destination = Guid.NewGuid();
    private readonly Guid _season = Guid.NewGuid();
    private readonly Package _safari;

    public EnquiryServiceTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Destinations.Add(new Destination { Id = _destination, Slug = "nairobi", Name = "Nairobi", Country = "KE" });
        _db.Seasons.Add(new Season
        {
            Id = _season, Slug = "peak", Name = "Peak",
            Ranges = [new SeasonRange
            {
                Id = Guid.NewGuid(), SeasonId = _season, StartDate = Today.AddDays(10), EndDate = Today.AddDays(60),
            }],
        });
        _safari = AddPackage("Sala's Classic Safari", PackageStatus.Published);
        _db.PackageRates.Add(new PackageRate
        {
            Id = Guid.NewGuid(), PackageId = _safari.Id, SeasonId = _season, Currency = "USD", PriceMinor = 450_000,
        });
        _db.SaveChanges();
        _service = new EnquiryService(_db, new CatalogueService(_db), _queue);
        _notifier = NewNotifier(notifyTo: "staff@example.com");
    }

    private readonly FakeQueue _queue = new();
    private readonly FakeEmail _mail = new();
    private readonly FakeMedia _media = new();
    private readonly EnquiryNotifier _notifier;

    private EnquiryNotifier NewNotifier(string notifyTo) => new(
        _db, _mail, _media,
        Options.Create(new EnquirySettings { NotifyTo = notifyTo }),
        Options.Create(new AppSettings { FrontendBaseUrl = "https://www.example.com/" }));

    /// <summary>Sends the email for the newest queued enquiry, as the background worker would.</summary>
    private Task Announce(EnquiryNotifier? notifier = null) => (notifier ?? _notifier).SendAsync(_queue.Ids.Last());

    public void Dispose() => _db.Dispose();

    private sealed class FakeQueue : IEnquiryNotificationQueue
    {
        public List<Guid> Ids { get; } = [];
        public void Enqueue(Guid enquiryId) => Ids.Add(enquiryId);
        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeEmail : IEmailService
    {
        public List<(string To, EnquiryNotice Notice)> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task SendInviteAsync(string toEmail, string plainToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string toEmail, string plainToken, CancellationToken ct = default) => Task.CompletedTask;

        public Task SendEnquiryNotificationAsync(string toEmail, EnquiryNotice notice, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("The mail server said no.");
            Sent.Add((toEmail, notice));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMedia : IMediaStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public string? SniffContentType(ReadOnlySpan<byte> h) =>
            h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF ? "image/jpeg" : null;

        public Task<byte[]?> ReadAsync(string publicPath, long maxBytes, CancellationToken ct = default) =>
            Task.FromResult(Files.TryGetValue(publicPath, out var bytes) && bytes.Length <= maxBytes ? bytes : null);

        public Task<StoredImage> SaveAsync(string folder, Stream content, string contentType, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public void Delete(string publicPath) { }
    }

    [Fact]
    public async Task The_email_says_where_the_package_is_and_who_it_is_for()
    {
        await _service.CreateAsync(ForPackage());
        await Announce();

        var (_, notice) = Assert.Single(_mail.Sent);
        Assert.Equal("Nairobi", notice.Destination);
        Assert.Equal("2 adults", notice.Party);
        Assert.NotNull(notice.ReceivedAt);
        Assert.Null(notice.Photo); // the package has no hero photo yet
    }

    [Fact]
    public async Task A_small_hero_photo_goes_inside_the_email_and_a_big_one_is_left_out()
    {
        _db.PackageMedia.Add(new PackageMedia
        {
            Id = Guid.NewGuid(), PackageId = _safari.Id, Role = MediaRole.Hero, Path = "/media/packages/hero.jpg", SortKey = 0,
        });
        await _db.SaveChangesAsync();
        _media.Files["/media/packages/hero.jpg"] = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

        await _service.CreateAsync(ForPackage());
        await Announce();
        var photo = Assert.Single(_mail.Sent).Notice.Photo;
        Assert.Equal("image/jpeg", photo?.ContentType);

        _mail.Sent.Clear();
        _media.Files["/media/packages/hero.jpg"] = new byte[500 * 1024]; // over the 400 KB cap
        await _service.CreateAsync(ForPackage(checkIn: Today.AddDays(30)));
        await Announce();
        Assert.Null(Assert.Single(_mail.Sent).Notice.Photo);
    }

    [Fact]
    public async Task Staff_are_emailed_the_enquiry_with_a_link_to_the_admin()
    {
        var created = await _service.CreateAsync(ForPackage());
        await Announce();

        var (to, notice) = Assert.Single(_mail.Sent);
        var saved = await _db.Enquiries.SingleAsync();
        Assert.Equal("staff@example.com", to);
        Assert.Equal(created.Reference, notice.Reference);
        Assert.Equal("package enquiry", notice.Kind);
        Assert.Equal(("Jane Doe", "jane@example.com"), (notice.GuestName, notice.GuestEmail));
        Assert.Equal("Sala's Classic Safari", notice.PackageTitle);
        Assert.Equal("USD 4,500", notice.QuotedTotal);
        Assert.Equal($"https://www.example.com/admin/enquiries/{saved.Id}", notice.AdminLink);
    }

    [Fact]
    public async Task A_general_enquiry_is_emailed_too_without_a_package_or_total()
    {
        await _service.CreateAsync(General());
        await Announce();

        var (_, notice) = Assert.Single(_mail.Sent);
        Assert.Equal("general", notice.Kind);
        Assert.Null(notice.PackageTitle);
        Assert.Null(notice.QuotedTotal);
        Assert.Equal("Group booking", notice.Subject);
    }

    [Fact]
    public async Task A_failing_mail_server_does_not_lose_the_enquiry()
    {
        _mail.Fail = true;

        var created = await _service.CreateAsync(ForPackage()); // the guest's request never touches the mail server

        Assert.Equal(1, await _db.Enquiries.CountAsync());
        Assert.StartsWith("ENQ-", created.Reference);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Announce()); // the worker sees it, to retry or log
    }

    [Fact]
    public async Task No_address_configured_means_no_email()
    {
        await _service.CreateAsync(ForPackage());

        await Announce(NewNotifier(notifyTo: ""));

        Assert.Empty(_mail.Sent);
        Assert.Equal(1, await _db.Enquiries.CountAsync());
    }

    [Fact]
    public async Task A_saved_enquiry_is_queued_once_and_bots_and_repeats_are_not_queued()
    {
        await _service.CreateAsync(ForPackage(website: "http://spam.example"));
        Assert.Empty(_queue.Ids);

        await _service.CreateAsync(ForPackage());
        await _service.CreateAsync(ForPackage());

        var saved = await _db.Enquiries.SingleAsync();
        Assert.Equal([saved.Id], _queue.Ids);
    }

    [Fact]
    public async Task An_enquiry_that_has_gone_is_not_emailed()
    {
        await _notifier.SendAsync(Guid.NewGuid());

        Assert.Empty(_mail.Sent);
    }

    private Package AddPackage(string title, PackageStatus status)
    {
        var package = new Package
        {
            Id = Guid.NewGuid(), Slug = title.ToLowerInvariant().Replace("'", "").Replace(' ', '-'), Title = title,
            Status = status, Category = "SAFARI", DestinationId = _destination, Nights = 3, MinNights = 3, Adults = 2,
            BaseCurrency = "USD", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        _db.Packages.Add(package);
        return package;
    }

    private CreateEnquiryRequest ForPackage(string? slug = null, DateOnly? checkIn = null, string email = "jane@example.com",
        string? website = null) =>
        new(EnquiryType.Package, slug ?? _safari.Slug, checkIn ?? Today.AddDays(20), 3, "Jane Doe", email, "+254700000000",
            null, "Two children travelling.", true, "/contact?package=Sala%27s+Classic+Safari", website);

    private static CreateEnquiryRequest General(string email = "sam@example.com") =>
        new(EnquiryType.General, null, null, null, "Sam Roe", email, null, "Group booking", "Ten of us in June.", true, null, null);

    [Fact]
    public async Task A_package_enquiry_is_saved_with_the_apis_own_quote()
    {
        var created = await _service.CreateAsync(ForPackage());

        Assert.Equal($"ENQ-{DateTime.UtcNow.Year}-0001", created.Reference);
        var saved = await _db.Enquiries.SingleAsync();
        Assert.Equal(EnquiryStatus.New, saved.Status);
        Assert.Equal(_safari.Id, saved.PackageId);
        Assert.Equal("Sala's Classic Safari", saved.PackageTitle);
        Assert.Equal(450_000, saved.QuotedTotalMinor);
        Assert.Equal("USD", saved.Currency);
        Assert.Equal(3, saved.Nights);
        Assert.Equal("Jane Doe", saved.Name);
        var audit = await _db.AuditLogs.SingleAsync();
        Assert.Equal(("Enquiry.Created", "Enquiry", saved.Id.ToString()), (audit.Action, audit.EntityType, audit.EntityId));
        Assert.Null(audit.ActorId);
    }

    [Fact]
    public async Task References_count_up_through_the_year()
    {
        var first = await _service.CreateAsync(ForPackage());
        var second = await _service.CreateAsync(General());

        Assert.EndsWith("-0001", first.Reference);
        Assert.EndsWith("-0002", second.Reference);
    }

    [Fact]
    public async Task A_date_without_a_rate_is_saved_without_a_total()
    {
        var created = await _service.CreateAsync(ForPackage(checkIn: Today.AddDays(200)));

        var saved = await _db.Enquiries.SingleAsync(e => e.Reference == created.Reference);
        Assert.Null(saved.QuotedTotalMinor);
        Assert.Null(saved.Currency);
        Assert.Equal(Today.AddDays(200), saved.CheckIn);
    }

    [Fact]
    public async Task A_package_enquiry_without_dates_is_saved_without_a_quote()
    {
        var created = await _service.CreateAsync(ForPackage() with { CheckIn = null, Nights = null });

        var saved = await _db.Enquiries.SingleAsync(e => e.Reference == created.Reference);
        Assert.Equal(_safari.Id, saved.PackageId);
        Assert.Null(saved.CheckIn);
        Assert.Null(saved.QuotedTotalMinor);
    }

    [Fact]
    public async Task A_past_date_or_too_few_nights_is_the_guests_to_fix()
    {
        await Assert.ThrowsAsync<QuoteException>(() => _service.CreateAsync(ForPackage(checkIn: Today.AddDays(-2))));
        await Assert.ThrowsAsync<QuoteException>(() => _service.CreateAsync(ForPackage() with { Nights = 1 }));

        Assert.Empty(_db.Enquiries);
    }

    [Fact]
    public async Task Only_a_published_package_can_be_enquired_about()
    {
        var draft = AddPackage("Quiet Draft", PackageStatus.Draft);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(ForPackage(draft.Slug)));
        await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(ForPackage("no-such-package")));
        Assert.Empty(_db.Enquiries);
    }

    [Fact]
    public async Task A_general_enquiry_needs_no_package()
    {
        var created = await _service.CreateAsync(General());

        var saved = await _db.Enquiries.SingleAsync(e => e.Reference == created.Reference);
        Assert.Equal(EnquiryType.General, saved.Type);
        Assert.Null(saved.PackageId);
        Assert.Equal("Group booking", saved.Subject);
    }

    [Fact]
    public async Task A_filled_honeypot_gets_a_reference_and_saves_nothing()
    {
        var created = await _service.CreateAsync(ForPackage(website: "http://spam.example"));

        Assert.StartsWith("ENQ-", created.Reference);
        Assert.Empty(_db.Enquiries);
        Assert.Empty(_db.AuditLogs);
    }

    [Fact]
    public async Task The_same_enquiry_sent_twice_is_saved_once()
    {
        var first = await _service.CreateAsync(ForPackage());
        var again = await _service.CreateAsync(ForPackage(email: "JANE@example.com"));

        Assert.Equal(first.Reference, again.Reference);
        Assert.Equal(1, await _db.Enquiries.CountAsync());
    }

    [Fact]
    public async Task A_different_date_is_a_new_enquiry()
    {
        await _service.CreateAsync(ForPackage());
        var other = await _service.CreateAsync(ForPackage(checkIn: Today.AddDays(30)));

        Assert.EndsWith("-0002", other.Reference);
    }

    [Fact]
    public async Task The_list_filters_by_status_and_counts_every_status()
    {
        await _service.CreateAsync(ForPackage());
        var second = await _service.CreateAsync(General());
        await _service.CreateAsync(General("lee@example.com"));
        var target = await _db.Enquiries.SingleAsync(e => e.Reference == second.Reference);
        await _service.SetStatusAsync(target.Id, new UpdateEnquiryRequest(EnquiryStatus.Contacted), Guid.NewGuid());

        var contacted = await _service.ListAsync("contacted", 1, 25);
        var everything = await _service.ListAsync(null, 1, 2);

        Assert.Equal(1, contacted.Total);
        Assert.Equal(second.Reference, contacted.Items.Single().Reference);
        Assert.Equal("Group booking", contacted.Items.Single().Subject);
        Assert.Equal((3, 2, 1, 0), (contacted.Counts.All, contacted.Counts.New, contacted.Counts.Contacted, contacted.Counts.Closed));
        Assert.Equal((3, 2), (everything.Total, everything.Items.Count));
        Assert.True(everything.Items[0].CreatedAt >= everything.Items[1].CreatedAt);
    }

    [Fact]
    public async Task Changing_the_status_records_who_and_writes_an_audit_row()
    {
        await _service.CreateAsync(General());
        var id = (await _db.Enquiries.SingleAsync()).Id;
        var staff = Guid.NewGuid();

        var updated = await _service.SetStatusAsync(id, new UpdateEnquiryRequest(EnquiryStatus.Contacted), staff);

        Assert.Equal(EnquiryStatus.Contacted, updated!.Status);
        Assert.Equal(staff, updated.HandledById);
        var audit = await _db.AuditLogs.SingleAsync(a => a.Action == "Enquiry.StatusChanged");
        Assert.Equal(("New", "Contacted", staff.ToString()), (audit.Before, audit.After, audit.ActorId));

        var reopened = await _service.SetStatusAsync(id, new UpdateEnquiryRequest(EnquiryStatus.New), staff);
        Assert.Null(reopened!.HandledById);
    }

    [Fact]
    public async Task Setting_the_status_it_already_has_changes_nothing()
    {
        await _service.CreateAsync(General());
        var id = (await _db.Enquiries.SingleAsync()).Id;

        await _service.SetStatusAsync(id, new UpdateEnquiryRequest(EnquiryStatus.New), Guid.NewGuid());

        Assert.Empty(_db.AuditLogs.Where(a => a.Action == "Enquiry.StatusChanged"));
    }

    [Fact]
    public async Task An_enquiry_that_does_not_exist_is_null()
    {
        Assert.Null(await _service.GetAsync(Guid.NewGuid()));
        Assert.Null(await _service.SetStatusAsync(Guid.NewGuid(), new UpdateEnquiryRequest(EnquiryStatus.Closed), Guid.NewGuid()));
    }
}

public class CreateEnquiryRequestValidatorTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly CreateEnquiryRequestValidator _validator = new();

    private static CreateEnquiryRequest Valid() =>
        new(EnquiryType.Package, "salas-classic-safari", Today.AddDays(5), 3, "Jane Doe", "jane@example.com", null, null,
            null, true, null, null);

    private string[] Failing(CreateEnquiryRequest request) =>
        _validator.Validate(request).Errors.Select(e => e.PropertyName).Distinct().ToArray();

    [Fact]
    public void A_complete_package_enquiry_is_valid() => Assert.Empty(Failing(Valid()));

    [Fact]
    public void Consent_is_required() => Assert.Equal(["Consent"], Failing(Valid() with { Consent = false }));

    [Fact]
    public void A_bad_email_is_refused() => Assert.Equal(["Email"], Failing(Valid() with { Email = "not-an-email" }));

    [Fact]
    public void A_package_enquiry_needs_a_slug() => Assert.Equal(["Slug"], Failing(Valid() with { Slug = null }));

    [Fact]
    public void A_past_check_in_is_refused() =>
        Assert.Equal(["CheckIn"], Failing(Valid() with { CheckIn = Today.AddDays(-1) }));

    [Fact]
    public void Nights_stay_within_sensible_bounds() =>
        Assert.Equal(["Nights"], Failing(Valid() with { Nights = 0 }));

    [Fact]
    public void Anything_but_a_package_enquiry_needs_a_subject_and_a_message() =>
        Assert.Equal(["Subject", "Message"],
            Failing(Valid() with { Type = EnquiryType.General, Slug = null }).Order().Reverse().ToArray());

    [Fact]
    public void An_overlong_message_is_refused() =>
        Assert.Equal(["Message"], Failing(Valid() with { Message = new string('x', 4001) }));
}

public class EnquiryEmailTests
{
    [Fact]
    public void The_email_lists_what_staff_need_and_says_where_to_reply()
    {
        var body = EmailService.EnquiryBody(new EnquiryNotice(
            "ENQ-2026-0001", "package enquiry", "Jane Doe", "jane@example.com", "+254700000000",
            "Sala's Classic Safari", new DateOnly(2026, 10, 1), 3, "USD 4,500", null, "Two children.",
            "https://www.example.com/admin/enquiries/abc"));

        Assert.Contains("ENQ-2026-0001", body);
        Assert.Contains("jane@example.com", body);
        Assert.Contains("+254700000000", body);
        Assert.Contains("Sala's Classic Safari", body);
        Assert.Contains("1 October 2026", body);
        Assert.Contains("USD 4,500", body);
        Assert.Contains("Two children.", body);
        Assert.Contains("https://www.example.com/admin/enquiries/abc", body);
    }

    [Fact]
    public void A_date_without_a_price_says_so()
    {
        var body = EmailService.EnquiryBody(new EnquiryNotice(
            "ENQ-2026-0002", "package enquiry", "Jane", "j@example.com", null, "Safari", new DateOnly(2027, 1, 1), 2,
            null, null, null, "https://x/admin/enquiries/1"));

        Assert.Contains("no online price for these dates", body);
    }

    [Theory]
    [InlineData(587, true, SecureSocketOptions.StartTls)]
    [InlineData(465, true, SecureSocketOptions.SslOnConnect)]
    [InlineData(25, false, SecureSocketOptions.None)]
    public void The_port_decides_how_the_connection_is_secured(int port, bool ssl, SecureSocketOptions expected) =>
        Assert.Equal(expected, EmailService.SecurityFor(new EmailSettings { SmtpPort = port, EnableSsl = ssl }));
}

public class EnquiryEmailHtmlTests
{
    private static EnquiryNotice Package(string name = "Jane Doe", string? message = "Two adults and two children.\nWe would like a river view.") => new(
        "ENQ-2026-0007", "package enquiry", name, "jane@example.com", "+254 700 000 000", "Sala's Classic Safari",
        new DateOnly(2026, 10, 1), 3, "USD 17,750", null, message, "https://www.example.com/admin/enquiries/abc",
        Destination: "Nairobi", Party: "2 adults, 2 children", ReceivedAt: new DateTime(2026, 9, 30, 19, 51, 0, DateTimeKind.Utc));

    private static EnquiryNotice General() => new(
        "ENQ-2026-0008", "career", "Amara Nwosu", "amara@example.com", null, null, null, null, null,
        "Operations role", "Applying for the coordinator role.", "https://www.example.com/admin/enquiries/def");

    [Fact]
    public void A_package_enquiry_leads_with_the_package_and_shows_the_trip_and_price()
    {
        var html = EnquiryEmailTemplate.Html(Package(), "cid:logo");

        Assert.Contains("<h1", html);
        Assert.Contains("Sala&#39;s Classic Safari", html);
        Assert.Contains("1 October 2026", html);
        Assert.Contains("USD 17,750", html);
        Assert.Contains("ENQ-2026-0007", html);
        Assert.Contains("href=\"https://www.example.com/admin/enquiries/abc\"", html);
        Assert.Contains("mailto:jane@example.com?subject=Your%20ARLink28%20enquiry%20ENQ-2026-0007", html);
        Assert.Contains("Reply to Jane", html);
        Assert.Contains("src=\"cid:logo\"", html);
        Assert.Contains("We would like a river view.", html);
        Assert.Contains("<br>", html); // a line break in the message is kept
    }

    [Fact]
    public void The_summary_shows_check_in_check_out_and_the_stay_like_a_booking()
    {
        var html = EnquiryEmailTemplate.Html(Package(), "cid:logo");

        Assert.Contains("1 Oct 2026", html);
        Assert.Contains("Thursday", html);
        Assert.Contains("4 Oct 2026", html); // check-in plus three nights
        Assert.Contains("Sunday", html);
        Assert.Contains("3 nights", html);
        Assert.Contains("Nairobi", html);
        Assert.Contains("Packaged for 2 adults, 2 children", html);
        Assert.Contains("Received 30 Sep 2026 at 19:51 UTC", html);
    }

    [Fact]
    public void The_photo_is_shown_only_when_there_is_one()
    {
        Assert.DoesNotContain("src=\"cid:photo\"", EnquiryEmailTemplate.Html(Package(), "cid:logo"));
        Assert.Contains("src=\"cid:photo\"", EnquiryEmailTemplate.Html(Package(), "cid:logo", "cid:photo"));
    }

    [Fact]
    public void The_guest_card_has_initials_and_the_phone_links_to_a_call_and_whatsapp()
    {
        var html = EnquiryEmailTemplate.Html(Package(), "cid:logo");

        Assert.Contains(">JD<", html);
        Assert.Contains("href=\"tel:+254 700 000 000\"", html);
        Assert.Contains("https://wa.me/254700000000", html);
    }

    [Fact]
    public void The_footer_carries_the_company_details()
    {
        var html = EnquiryEmailTemplate.Html(Package(), "cid:logo");

        Assert.Contains("Mulliner Towers", html);
        Assert.Contains("Covent Garden", html);
        Assert.Contains("support@arlinks.com", html);
    }

    [Fact]
    public void Nothing_a_guest_types_can_add_markup_to_the_email()
    {
        var html = EnquiryEmailTemplate.Html(
            Package(name: "<script>alert(1)</script>", message: "<img src=x onerror=alert(1)> & more"), "cid:logo");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt; &amp; more", html);
    }

    [Fact]
    public void A_general_enquiry_leads_with_its_subject_and_has_no_trip_table()
    {
        var html = EnquiryEmailTemplate.Html(General(), "cid:logo");

        Assert.Contains("Operations role", html);
        Assert.Contains("Career", html);
        Assert.Contains(">AN<", html);
        Assert.DoesNotContain("wa.me", html); // no phone, so no WhatsApp button
        Assert.DoesNotContain("Check-in", html);
        Assert.DoesNotContain("Price at the time", html);
        Assert.DoesNotContain("Phone", html); // none was given
    }

    [Fact]
    public void A_date_without_a_price_says_so_plainly()
    {
        var html = EnquiryEmailTemplate.Html(Package() with { QuotedTotal = null }, "cid:logo");

        Assert.Contains("No online price", html);
    }

    [Fact]
    public void Writes_previews_to_look_at_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_DIR");
        if (string.IsNullOrEmpty(dir)) return; // only when someone sets it: dotnet test with EMAIL_PREVIEW_DIR=some-folder

        var logo = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_LOGO") ?? "";
        Directory.CreateDirectory(dir);
        var photo = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_PHOTO");
        File.WriteAllText(Path.Combine(dir, "package.html"), EnquiryEmailTemplate.Html(Package(), logo, photo));
        File.WriteAllText(Path.Combine(dir, "package-nophoto.html"), EnquiryEmailTemplate.Html(Package(), logo));
        File.WriteAllText(Path.Combine(dir, "general.html"), EnquiryEmailTemplate.Html(General(), logo));
    }
}

public class EnquiryNotificationWorkerTests
{
    private sealed class FakeNotifier(int failTimes) : IEnquiryNotifier
    {
        private int _calls;
        public List<Guid> Sent { get; } = [];

        public Task SendAsync(Guid enquiryId, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _calls) <= failTimes) throw new InvalidOperationException("The mail server is down.");
            lock (Sent) Sent.Add(enquiryId);
            return Task.CompletedTask;
        }
    }

    private static (EnquiryNotificationWorker Worker, EnquiryNotificationQueue Queue, FakeNotifier Notifier) Build(int failTimes)
    {
        var notifier = new FakeNotifier(failTimes);
        var provider = new ServiceCollection().AddSingleton<IEnquiryNotifier>(notifier).BuildServiceProvider();
        var queue = new EnquiryNotificationQueue();
        var worker = new EnquiryNotificationWorker(queue, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EnquiryNotificationWorker>.Instance) { RetryDelay = TimeSpan.FromMilliseconds(5) };
        return (worker, queue, notifier);
    }

    private static async Task WaitUntil(Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++) await Task.Delay(10);
    }

    [Fact]
    public async Task A_queued_enquiry_is_sent()
    {
        var (worker, queue, notifier) = Build(failTimes: 0);
        await worker.StartAsync(CancellationToken.None);
        var id = Guid.NewGuid();

        queue.Enqueue(id);
        await WaitUntil(() => notifier.Sent.Count == 1);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal([id], notifier.Sent);
    }

    [Fact]
    public async Task A_failing_send_is_retried_until_it_goes_through()
    {
        var (worker, queue, notifier) = Build(failTimes: 2);
        await worker.StartAsync(CancellationToken.None);
        var id = Guid.NewGuid();

        queue.Enqueue(id);
        await WaitUntil(() => notifier.Sent.Count == 1);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal([id], notifier.Sent); // two failures, then the third attempt
    }

    [Fact]
    public async Task After_three_failed_attempts_it_gives_up_on_that_one_and_carries_on()
    {
        var (worker, queue, notifier) = Build(failTimes: EnquiryNotificationWorker.Attempts);
        await worker.StartAsync(CancellationToken.None);
        var lost = Guid.NewGuid();
        var next = Guid.NewGuid();

        queue.Enqueue(lost);
        queue.Enqueue(next);
        await WaitUntil(() => notifier.Sent.Count == 1);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal([next], notifier.Sent);
    }
}
