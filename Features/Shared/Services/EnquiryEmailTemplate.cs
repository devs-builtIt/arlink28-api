using System.Globalization;
using System.Net;
using System.Text;
using Arlink28.Api.Features.Shared.Services.Interfaces;

namespace Arlink28.Api.Features.Shared.Services;

/// <summary>
/// The enquiry notification as HTML. Email clients ignore modern CSS, so this is a table layout with
/// inline styles: 600 wide, the package photo first, the trip and price as one summary block, the guest
/// as a card, one red button, and the company's details in the foot. The few rules in the head only
/// stack the columns on a phone. Everything a guest typed is HTML-encoded before it goes in.
/// </summary>
public static class EnquiryEmailTemplate
{
    private const string Navy = "#05090f";
    private const string Ink = "#0b1726";
    private const string Body = "#33425a";
    private const string Muted = "#5b6b82";
    private const string Line = "#e3e8ee";
    private const string LineStrong = "#cdd5e0";
    private const string Canvas = "#eef1f5";
    private const string Wash = "#f7f9fb";
    private const string Red = "#e61e2b";
    private const string Font = "'Helvetica Neue',Helvetica,Arial,sans-serif";

    // The company's details, as the website's footer and contact page give them.
    private const string Tagline = "Connecting African journeys through travel, tourism and the future of aviation.";
    private const string Lagos = "2nd Floor, Office 316B, Mulliner Towers, 39 Alfred Rewane Road, Ikoyi, Lagos, Nigeria 101233";
    private const string London = "71–75 Shelton Street, Covent Garden, London WC2H 9JQ, United Kingdom";
    private const string SupportEmail = "support@arlinks.com";
    private const string SupportPhone = "+234 704 700 9128";

    public static string Html(EnquiryNotice n, string logoSrc, string? photoSrc = null)
    {
        var isPackage = n.PackageTitle is not null;
        var heading = n.PackageTitle ?? n.Subject ?? Capitalise(n.Kind);
        var first = n.GuestName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? n.GuestName;
        var replyHref = $"mailto:{n.GuestEmail}?subject={Uri.EscapeDataString($"Your ARLink28 enquiry {n.Reference}")}";
        var whatsapp = WhatsApp(n.Phone);

        var preheader = isPackage
            ? $"{n.GuestName}, {Join(n.CheckIn is { } c ? Day(c) : null, Stay(n.Nights), n.QuotedTotal)}"
            : $"{n.GuestName}: {Clip(n.Message ?? n.Subject ?? n.Kind, 90)}";
        var received = n.ReceivedAt is { } at
            ? $"Received {at.ToString("d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture)} UTC"
            : "";
        var sub = isPackage ? Join(n.Party is null ? null : $"Packaged for {n.Party}", Stay(n.Nights)) : Capitalise(n.Kind);

        // Pieces that may be absent, built here so the template below stays readable.
        var kicker = n.Destination is null ? "" : $"<p style=\"margin:0 0 6px;font-size:14px;color:{Muted};\">{E(n.Destination)}</p>";
        var subline = string.IsNullOrEmpty(sub) ? "" : $"<p style=\"margin:8px 0 0;font-size:15px;color:{Muted};\">{E(sub)}</p>";
        var whatsappButton = whatsapp is null ? "" :
            "<td class=\"stack gap\" valign=\"top\"><a class=\"btn\" href=\"" + E(whatsapp) + "\" style=\"display:inline-block;border:1px solid "
            + LineStrong + ";border-radius:8px;padding:13px 22px;font-family:" + Font + ";font-size:15px;font-weight:700;color:" + Ink
            + ";text-decoration:none;\">WhatsApp</a></td>";

        var page = new StringBuilder();
        page.Append($$"""
            <!DOCTYPE html>
            <html lang="en"><head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light">
            <meta name="supported-color-schemes" content="light">
            <title>{{E(n.Reference)}}</title>
            <style>
              @media only screen and (max-width:620px) {
                .wrap { padding: 0 !important; }
                .pad { padding-left: 20px !important; padding-right: 20px !important; }
                .stack { display: block !important; width: 100% !important; box-sizing: border-box; }
                .cut { border-left: 0 !important; border-top: 1px solid {{Line}} !important; }
                .h1 { font-size: 25px !important; }
                .btn { display: block !important; text-align: center !important; }
                .gap { padding: 10px 0 0 0 !important; }
                .left-m { text-align: left !important; }
                .full { width: 100% !important; }
                .round-top { border-radius: 0 !important; }
              }
            </style>
            </head>
            <body style="margin:0;padding:0;background:{{Canvas}};">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:{{Canvas}};">{{E(preheader)}}</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{{Canvas}};">
            <tr><td align="center" class="wrap" style="padding:24px 12px;">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;background:#ffffff;border-radius:10px;" class="round-top">

            <tr><td class="pad round-top" style="background:{{Navy}};padding:18px 32px;border-radius:10px 10px 0 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
                <td width="56" valign="middle"><img src="{{E(logoSrc)}}" width="44" height="28" alt="" style="display:block;border:0;"></td>
                <td valign="middle" style="font-family:{{Font}};font-size:19px;font-weight:700;color:#ffffff;">ARLink28</td>
                <td align="right" valign="middle" style="font-family:{{Font}};font-size:13px;color:#8b9cb5;">Enquiry</td>
              </tr></table>
            </td></tr>

            <tr><td class="pad" style="background:{{Wash}};padding:12px 32px;border-bottom:1px solid {{Line}};">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
                <td class="stack" valign="middle" style="font-family:{{Font}};font-size:14px;color:{{Ink}};white-space:nowrap;">
                  <span style="display:inline-block;background:#fdf3dc;color:#8a5a00;font-size:12px;font-weight:700;line-height:22px;padding:0 9px;border-radius:6px;">New</span>
                  <span style="padding-left:8px;font-weight:700;">{{E(n.Reference)}}</span>
                </td>
                <td class="stack left-m gap" align="right" valign="middle" style="font-family:{{Font}};font-size:13px;color:{{Muted}};">{{E(received)}}</td>
              </tr></table>
            </td></tr>
            """);

        if (photoSrc is not null)
        {
            page.Append($$"""
                <tr><td style="background:#dde3ea;padding:0;line-height:0;font-size:0;">
                  <div style="max-height:250px;overflow:hidden;"><img src="{{E(photoSrc)}}" width="600" alt="{{E(n.PackageTitle)}}" style="display:block;width:100%;height:auto;border:0;"></div>
                </td></tr>
                """);
        }

        page.Append($$"""
            <tr><td class="pad" style="padding:30px 32px 6px;font-family:{{Font}};color:{{Body}};">
              {{kicker}}
              <h1 class="h1" style="margin:0;font-size:28px;line-height:1.2;font-weight:700;color:{{Ink}};letter-spacing:-0.4px;">{{E(heading)}}</h1>
              {{subline}}
            </td></tr>
            """);

        if (isPackage) page.Append(Summary(n));

        page.Append(Guest(n));

        if (n.Subject is not null && isPackage)
            page.Append($$"""<tr><td class="pad" style="padding:22px 32px 0;font-family:{{Font}};font-size:16px;font-weight:700;color:{{Ink}};">{{E(n.Subject)}}</td></tr>""");

        if (n.Message is not null)
        {
            page.Append($$"""
                <tr><td class="pad" style="padding:24px 32px 0;font-family:{{Font}};">
                  <p style="margin:0 0 10px;font-size:15px;font-weight:700;color:{{Ink}};">What {{E(first)}} wrote</p>
                  <div style="background:{{Wash}};border:1px solid {{Line}};border-radius:8px;padding:16px 18px;font-size:16px;line-height:1.6;color:{{Body}};">{{E(n.Message).ReplaceLineEndings("<br>")}}</div>
                </td></tr>
                """);
        }

        page.Append($$"""
            <tr><td class="pad" style="padding:30px 32px 34px;">
              <table role="presentation" class="full" cellpadding="0" cellspacing="0" border="0"><tr>
                <td class="stack" valign="top" style="padding-right:10px;">
                  <a class="btn" href="{{E(n.AdminLink)}}" style="display:inline-block;background:{{Red}};border-radius:8px;padding:14px 26px;font-family:{{Font}};font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;">Open in the admin</a>
                </td>
                <td class="stack gap" valign="top" style="padding-right:10px;">
                  <a class="btn" href="{{E(replyHref)}}" style="display:inline-block;border:1px solid {{LineStrong}};border-radius:8px;padding:13px 22px;font-family:{{Font}};font-size:15px;font-weight:700;color:{{Ink}};text-decoration:none;">Reply to {{E(first)}}</a>
                </td>
                {{whatsappButton}}
              </tr></table>
            </td></tr>

            <tr><td class="pad round-top" style="background:{{Navy}};padding:30px 32px;border-radius:0 0 10px 10px;font-family:{{Font}};color:#c6d0de;">
              <p style="margin:0 0 4px;font-size:17px;font-weight:700;color:#ffffff;">ARLink28</p>
              <p style="margin:0 0 20px;font-size:14px;line-height:1.5;color:#8b9cb5;">{{E(Tagline)}}</p>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
                <td class="stack" valign="top" style="width:50%;padding:0 16px 0 0;font-size:13px;line-height:1.55;color:#c6d0de;"><span style="color:#ffffff;font-weight:700;">Lagos</span><br>{{E(Lagos)}}</td>
                <td class="stack gap" valign="top" style="width:50%;padding:0;font-size:13px;line-height:1.55;color:#c6d0de;"><span style="color:#ffffff;font-weight:700;">London</span><br>{{E(London)}}</td>
              </tr></table>
              <p style="margin:18px 0 0;font-size:13px;color:#c6d0de;"><a href="mailto:{{SupportEmail}}" style="color:#ffffff;text-decoration:underline;">{{SupportEmail}}</a>&nbsp;&nbsp;&nbsp;{{SupportPhone}}</p>
              <p style="margin:18px 0 0;padding-top:16px;border-top:1px solid #1c2636;font-size:12px;line-height:1.6;color:#8b9cb5;">An internal notification about an enquiry made on the ARLink28 website. Reply to this email to answer {{E(first)}} directly. The enquiry is also saved in the admin as {{E(n.Reference)}}.</p>
            </td></tr>

            </table>
            </td></tr></table>
            </body></html>
            """);
        return page.ToString();
    }

    /// <summary>Check-in, check-out and length side by side, then the price: what staff need to quote.</summary>
    private static string Summary(EnquiryNotice n)
    {
        var checkOut = n.CheckIn is { } ci && n.Nights is { } nt ? ci.AddDays(nt) : (DateOnly?)null;
        return $$"""
            <tr><td class="pad" style="padding:22px 32px 0;font-family:{{Font}};">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="border:1px solid {{LineStrong}};border-radius:10px;border-collapse:separate;">
                <tr>
                  {{Cell("Check-in", n.CheckIn, first: true)}}
                  {{Cell("Check-out", checkOut)}}
                  <td class="stack cut" valign="top" style="width:34%;padding:16px 18px;border-left:1px solid {{Line}};">
                    <div style="font-size:13px;color:{{Muted}};">Stay</div>
                    <div style="margin-top:4px;font-size:18px;font-weight:700;color:{{Ink}};">{{E(n.Nights is { } k ? Stay(k) : "Not given")}}</div>
                  </td>
                </tr>
                <tr><td colspan="3" style="padding:0;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{{Wash}};border-top:1px solid {{Line}};border-radius:0 0 9px 9px;"><tr>
                    <td valign="middle" style="padding:16px 18px;font-size:14px;color:{{Muted}};">Price at the time<br><span style="font-size:13px;">What the website quoted for these dates</span></td>
                    <td align="right" valign="middle" style="padding:16px 18px;font-size:{{(n.QuotedTotal is null ? 15 : 24)}}px;font-weight:700;color:{{Ink}};white-space:nowrap;">{{E(n.QuotedTotal ?? "No online price")}}</td>
                  </tr></table>
                </td></tr>
              </table>
            </td></tr>
            """;
    }

    private static string Cell(string label, DateOnly? date, bool first = false) => $$"""
        <td class="stack{{(first ? "" : " cut")}}" valign="top" style="width:33%;padding:16px 18px;{{(first ? "" : $"border-left:1px solid {Line};")}}">
          <div style="font-size:13px;color:{{Muted}};">{{label}}</div>
          <div style="margin-top:4px;font-size:18px;font-weight:700;color:{{Ink}};">{{E(date is { } d ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "Not given")}}</div>
          <div style="margin-top:2px;font-size:13px;color:{{Muted}};">{{E(date is { } w ? w.ToString("dddd", CultureInfo.InvariantCulture) : "")}}</div>
        </td>
        """;

    private static string Guest(EnquiryNotice n) => $$"""
        <tr><td class="pad" style="padding:26px 32px 0;font-family:{{Font}};">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
            <td width="58" valign="top">
              <div style="width:46px;height:46px;border-radius:23px;background:{{Navy}};color:#ffffff;font-size:16px;font-weight:700;line-height:46px;text-align:center;">{{E(Initials(n.GuestName))}}</div>
            </td>
            <td valign="top" style="font-size:15px;line-height:1.55;color:{{Body}};">
              <div style="font-size:17px;font-weight:700;color:{{Ink}};">{{E(n.GuestName)}}</div>
              <a href="mailto:{{E(n.GuestEmail)}}" style="color:{{Ink}};text-decoration:underline;">{{E(n.GuestEmail)}}</a>
              {{(n.Phone is null ? "" : $"<br><a href=\"tel:{E(n.Phone)}\" style=\"color:{Ink};text-decoration:underline;\">{E(n.Phone)}</a>")}}
            </td>
          </tr></table>
        </td></tr>
        """;

    private static string? WhatsApp(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        return digits.Length >= 8 ? $"https://wa.me/{digits}" : null;
    }

    private static string Initials(string name)
    {
        var letters = name.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0]));
        var text = string.Concat(letters);
        return text.Length > 0 ? text : "?";
    }

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
    private static string Day(DateOnly d) => d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
    private static string? Stay(int? nights) => nights is { } n ? $"{n} {(n == 1 ? "night" : "nights")}" : null;
    private static string Join(params string?[] parts) => string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p)));
    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
