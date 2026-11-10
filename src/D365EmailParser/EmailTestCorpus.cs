using System;
using System.Collections.Generic;

internal enum EmailKind { Single, Thread }

/// <summary>
/// One parser regression case. <see cref="Keep"/> = text of the newest message that must survive;
/// <see cref="Drop"/> = quoted history that must be removed. Parser-agnostic so the same corpus can
/// be replayed against an older parser build to detect regressions.
/// </summary>
internal sealed record EmailCase
{
    public required string Name { get; init; }
    public required EmailKind Kind { get; init; }
    public required string Html { get; init; }
    public string[] Keep { get; init; } = [];
    public string[] Drop { get; init; } = [];
    public int? LatestAttachments { get; init; }
    public bool ExpectEmpty { get; init; }

    /// <summary>Format the parser does not handle yet (failed before and after the separator fix).
    /// Reported as GAP instead of FAIL; clear the flag once the case passes.</summary>
    public bool KnownGap { get; init; }

    /// <summary>Returns null when the parser output satisfies the case, otherwise the reason.</summary>
    public string? Failure(Func<string, string> text, Func<string, string> textWithPlaceholders,
                           Func<string, int> latestAttachmentCount)
    {
        string plain, placed;
        int attachments;
        try
        {
            plain = text(Html);
            placed = textWithPlaceholders(Html);
            attachments = latestAttachmentCount(Html);
        }
        catch (Exception ex)
        {
            return $"threw {ex.GetType().Name}: {ex.Message}";
        }

        if (ExpectEmpty && plain.Length > 0)
            return $"expected empty output, got \"{Preview(plain)}\"";
        foreach (var k in Keep)
            if (!Has(plain, k) || !Has(placed, k))
                return $"lost \"{k}\" -> got \"{Preview(plain)}\"";
        foreach (var d in Drop)
            if (Has(plain, d) || Has(placed, d))
                return $"kept history \"{d}\" -> got \"{Preview(plain)}\"";
        if (LatestAttachments is int n && attachments != n)
            return $"expected {n} attachment(s) in the latest message, got {attachments}";
        return null;
    }

    private static bool Has(string s, string part) => s.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static string Preview(string s)
    {
        s = s.Replace("\r", "").Replace("\n", " | ");
        return s.Length <= 220 ? s : s.Substring(0, 220) + "...";
    }
}

/// <summary>
/// Regression corpus: single (non-concatenated) messages that must come back intact, and concatenated
/// threads from many clients/languages whose quoted history must be removed.
/// All names, accounts and amounts are fictional.
/// </summary>
internal static partial class EmailTestCorpus
{
    public static IReadOnlyList<EmailCase> Cases { get; } = [.. SingleMessages(), .. Threads()];

    private static EmailCase Single(string name, string html, params string[] keep)
        => new() { Name = name, Kind = EmailKind.Single, Html = html, Keep = keep };

    private static EmailCase Thread(string name, string html, string[] keep, string[] drop)
        => new() { Name = name, Kind = EmailKind.Thread, Html = html, Keep = keep, Drop = drop };

    // ---- Reconstruction of the reported email (Outlook Word HTML, Hebrew, RTL): ONE message whose
    //      transfer requests are separated by a typed line. Variants swap only the separator. ----
    private const string DashLine =
        """<p class="MsoNormal" dir="RTL"><span lang="HE">------------------------------------------------------------</span><o:p></o:p></p>""";
    private const string UnderscoreLine =
        """<p class="MsoNormal" dir="RTL"><span lang="HE">____________________________________________________________</span><o:p></o:p></p>""";

    private static string[] TransferKeep =>
        ["ישראל ישראלי", "סך של - 23,456", "שרה לוי", "לאישורי העברה אודה", "בברכה"];

    private static string TransferRequest(string separator, string history = "") => $"""
        <html dir="rtl"><head><meta http-equiv="Content-Type" content="text/html; charset=utf-8"></head>
        <body lang="HE" dir="RTL"><div class="WordSection1" dir="rtl">
        <p class="MsoNormal" dir="RTL"><b><u><span lang="HE">אחה"צ טובים</span></u></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><o:p>&nbsp;</o:p></p>
        <p class="MsoNormal" dir="RTL"><b><u><span lang="HE">אודה לטיפולכם הדחוף -</span></u></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><o:p>&nbsp;</o:p></p>
        <p class="MsoNormal" dir="RTL"><b><u><span lang="HE">יש לבצע העברות לנהנים כדלקמן:</span></u></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">העברת תשלום לטובת מר </span><b><span lang="HE" style="background:yellow">ישראל ישראלי</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">בנק </span><b><span lang="HE">לאומי</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">סניף </span><b><span lang="HE">100</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">ח-ן </span><b><span lang="HE">111111/11</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><b><span lang="HE">סך של - 12,345 ש"ח</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><o:p>&nbsp;</o:p></p>
        {separator}
        <p class="MsoNormal" dir="RTL"><o:p>&nbsp;</o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">העברת תשלום לטובת מר </span><b><span lang="HE" style="background:yellow">משה כהן</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">בנק </span><b><span lang="HE">לאומי</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">סניף </span><b><span lang="HE">200</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">ח-ן </span><b><span lang="HE">222222/22</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><b><span lang="HE">סך של - 23,456 ש"ח</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><o:p>&nbsp;</o:p></p>
        {separator}
        <p class="MsoNormal" dir="RTL"><span lang="HE">העברת </span><b><u><span lang="HE">יתרת הסכום הנותר בחשבון הנאמנות (בקיזוז עמלות העברה כמובן)</span></u></b><span lang="HE"> תשלום לטובת גב' </span><b><span lang="HE" style="background:yellow">שרה לוי</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">בנק </span><b><span lang="HE">דיסקונט</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">סניף </span><b><span lang="HE">300</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><span lang="HE">ח-ן 333333</span><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><o:p>&nbsp;</o:p></p>
        <p class="MsoNormal" dir="RTL"><b><span lang="HE" style="background:yellow">לאישורי העברה אודה</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><b><span lang="HE" style="background:yellow">בברכה</span></b><o:p></o:p></p>
        <p class="MsoNormal" dir="RTL"><b><span lang="HE" style="background:yellow">דנה</span></b><o:p></o:p></p>
        {history}
        </div></body></html>
        """;

    private static IEnumerable<EmailCase> SingleMessages() =>
    [
        Single("Plain single paragraph (no history)",
            """<html><body><div><p>Hi team, kicking off the project today.</p></div></body></html>""",
            "kicking off the project"),

        Single("Reported transfer request split by ----- lines", TransferRequest(DashLine), TransferKeep),
        Single("Reported transfer request split by _____ lines", TransferRequest(UnderscoreLine), TransferKeep),
        Single("Reported transfer request split by <hr>",
            TransferRequest("""<hr size="2" width="100%" align="center">"""), TransferKeep),
        Single("Reported transfer request split by ===== lines",
            TransferRequest("""<p class="MsoNormal" dir="RTL">==============================</p>"""), TransferKeep),
        Single("Reported transfer request split by ***** lines",
            TransferRequest("""<p class="MsoNormal" dir="RTL">******************************</p>"""), TransferKeep),
        Single("Reported transfer request split by em-dash lines",
            TransferRequest("""<p class="MsoNormal" dir="RTL">——————————————————————</p>"""), TransferKeep),
        Single("Reported transfer request, separator inside a Word table",
            TransferRequest("""<table class="MsoNormalTable" border="0"><tr><td><p class="MsoNormal">______________________________</p></td></tr></table>"""),
            TransferKeep),

        Single("Hebrew label:value fields between _____ lines",
            """<div dir="rtl"><p>נא לבצע את ההעברות הבאות:</p><p>__________________________</p><p>שם המוטב: ישראל ישראלי</p><p>בנק: לאומי, סניף: 100</p><p>__________________________</p><p>שם המוטב: משה כהן</p><p>בנק: הפועלים, סניף: 200</p><p>בברכה, דנה</p></div>""",
            "שם המוטב: ישראל ישראלי", "שם המוטב: משה כהן", "בברכה, דנה"),

        Single("Signature separated by _____ line",
            """<div><p>Hi team, the monthly report is attached.</p><p>Thanks,</p><p>____________________</p><p>John Smith | Finance Manager</p><p>Tel: +1-555-0100 | Mobile: +1-555-0101</p></div>""",
            "monthly report is attached", "John Smith | Finance Manager", "Mobile: +1-555-0101"),

        Single("Hebrew signature separated by _____ line",
            """<div dir="rtl"><p>מצורף הדוח החודשי.</p><p>________________</p><p>דנה כהן | מנהלת כספים</p><p>טל: 03-1234567 | נייד: 050-1234567</p></div>""",
            "מצורף הדוח החודשי", "טל: 03-1234567"),

        Single("Disclaimer after ----- line",
            """<p>Please find the quote below.</p><p>Total: 4,200 USD</p><p>-----------------------------------------</p><p>This e-mail is confidential and intended solely for the addressee.</p>""",
            "Total: 4,200 USD", "intended solely for the addressee"),

        Single("Travel request with From:/To:/Date: fields after ----- lines",
            """<div><p>Please book the following trip:</p><p>------------------------------</p><p>From: Tel Aviv</p><p>To: London</p><p>Date: 14/12/2026</p><p>------------------------------</p><p>From: London</p><p>To: Tel Aviv</p><p>Date: 18/12/2026</p><p>Thanks, Ofir</p></div>""",
            "Date: 14/12/2026", "Date: 18/12/2026", "Thanks, Ofir"),

        Single("Status lines containing 'Sent:' without a From: header",
            """<div><p>Status update:</p><p>Invoice 991 Sent: yes</p><p>Payment Received: no</p><p>________________</p><p>Next review: Friday</p></div>""",
            "Invoice 991 Sent: yes", "Next review: Friday"),

        Single("Gmail indent button (<blockquote style=margin:0 0 0 40px>)",
            """<div dir="ltr"><div>Onboarding steps:</div><blockquote style="margin:0 0 0 40px;border:none;padding:0px"><div>1. Sign the NDA</div><div>2. Send a copy of your ID</div></blockquote><div>Thanks, HR</div></div>""",
            "2. Send a copy of your ID", "Thanks, HR"),

        Single("Outlook web indent button (<blockquote style=margin-top:0>)",
            """<html><body dir="rtl"><div><p>יש לבצע העברות לנהנים כדלקמן:</p><p>העברת תשלום לטובת מר ישראל ישראלי - 12,345 ש"ח</p></div><blockquote style="margin-top:0;margin-bottom:0"><p>העברת תשלום לטובת מר משה כהן - 23,456 ש"ח</p><p>העברת יתרת הסכום לטובת גב' שרה לוי</p></blockquote></body></html>""",
            "ישראל ישראלי", "משה כהן", "שרה לוי"),

        Single("Policy text quoted in a styled <blockquote>",
            """<p>As stated in our refund policy:</p><blockquote style="border-left:3px solid #ccc;padding-left:8px"><p>Refunds are issued within 14 days.</p></blockquote><p>Let me know if you need anything else.</p>""",
            "within 14 days", "anything else"),

        Single("Hebrew indented <blockquote> right after 'כדלקמן:'",
            """<div dir="rtl"><p>יש לבצע העברות לנהנים כדלקמן:</p><blockquote style="margin:0 40px 0 0;border:none;padding:0px"><p>ישראל ישראלי - 12,345 ש"ח</p><p>משה כהן - 23,456 ש"ח</p></blockquote><p>בברכה, דנה</p></div>""",
            "משה כהן - 23,456", "בברכה, דנה"),

        Single("D365 editor indent (<p style=margin-left:40px>)",
            """<p>Steps:</p><p style="margin-left:40px">1. Open the case</p><p style="margin-left:40px">2. Attach the invoice</p><p>Regards</p>""",
            "2. Attach the invoice", "Regards"),

        Single("Invoice table with ----- and _____ rows",
            """<p>Order summary:</p><table border="1" cellpadding="4"><tr><td>Item</td><td>Qty</td></tr><tr><td>----------</td><td>-----</td></tr><tr><td>Chairs</td><td>12</td></tr><tr><td>__________</td><td>_____</td></tr><tr><td>Total items</td><td>12</td></tr></table><p>Please confirm the order.</p>""",
            "Total items", "Please confirm the order"),

        Single("Fill-in form with _____ blanks",
            """<p>Please sign and return:</p><ol><li>Name: ______________</li><li>Signature: ______________</li><li>Date: ______________</li></ol><p>Thank you, Legal team</p>""",
            "Signature:", "Thank you, Legal team"),

        Single("Inline image + ----- line",
            """<p>See the screenshot of the error:</p><p><img src="cid:image001.png@01DB0001" data-attachment-id="aaaaaaaa-0000-0000-0000-000000000001" alt="error.png" width="400" height="200"></p><p>---------------------------</p><p>Steps to reproduce: open the case form.</p>""",
            "Steps to reproduce") with { LatestAttachments = 1 },

        Single("Two inline images split by _____ line",
            """<p>Before:</p><p><img src="cid:before.png@01DB0002" data-attachment-id="aaaaaaaa-0000-0000-0000-000000000002" alt="before.png"></p><p>______________________</p><p>After:</p><p><img src="cid:after.png@01DB0003" data-attachment-id="aaaaaaaa-0000-0000-0000-000000000003" alt="after.png"></p><p>Thanks</p>""",
            "After:", "Thanks") with { LatestAttachments = 2 },

        Single("Plain-text body (no HTML) with ----- lines",
            """
            Hello,

            Please transfer:
            -----------------
            Name: Israel Israeli
            Amount: 12,345
            -----------------
            Name: Moshe Cohen
            Amount: 23,456

            Thanks, Dana
            """,
            "Name: Moshe Cohen", "Thanks, Dana"),

        Single("Plain-text body (no HTML) with _____ lines",
            """
            Hello,

            Please transfer:
            _________________
            Name: Israel Israeli
            Amount: 12,345
            _________________
            Name: Moshe Cohen
            Amount: 23,456

            Thanks, Dana
            """,
            "Name: Moshe Cohen", "Thanks, Dana"),

        Single("<pre> body with ===== lines",
            """
            <pre>Daily report
            =====================
            Calls: 42
            =====================
            Tickets closed: 17</pre>
            """,
            "Calls: 42", "Tickets closed: 17"),

        Single("<hr> followed by 'From the desk of the CEO'",
            """<p>Team,</p><hr><p>From the desk of the CEO: great quarter!</p><p>Sent with thanks to everyone.</p>""",
            "great quarter", "Sent with thanks"),

        Single("Prose mentioning 'original message' / 'forwarded message'",
            """<p>As mentioned in my original message, the deadline is Friday.</p><p>Forwarded message counts are attached.</p>""",
            "deadline is Friday", "Forwarded message counts"),

        Single("'Sent from my iPhone' footer",
            """<div dir="ltr">Approved.</div><div><br></div><div dir="ltr">Sent from my iPhone</div>""",
            "Approved.", "Sent from my iPhone"),

        Single("Short --- and *** markers",
            """<p>Agenda</p><p>---</p><p>1. Budget</p><p>***</p><p>2. Hiring</p>""",
            "1. Budget", "2. Hiring"),

        Single("Newsletter with several <hr>",
            """<h1>November update</h1><hr><h2>Sales</h2><p>Up 5%.</p><hr><h2>Hiring</h2><p>Two new engineers joined.</p><hr><p>Unsubscribe</p>""",
            "Up 5%", "Two new engineers joined", "Unsubscribe"),

        new EmailCase { Name = "Empty body", Kind = EmailKind.Single, Html = "", ExpectEmpty = true },

        Single("Arabic label:value fields between _____ lines",
            """<div dir="rtl"><p>يرجى تنفيذ التحويلات التالية:</p><p>______________________</p><p>الاسم: أحمد علي</p><p>البنك: العربي</p><p>______________________</p><p>الاسم: سارة حسن</p><p>البنك: لئومي</p><p>مع التحية</p></div>""",
            "الاسم: أحمد علي", "الاسم: سارة حسن", "مع التحية"),

        Single("Nested wrappers with ----- line",
            """<div><div><div><p>Section A: budget approved.</p><p>------</p><p>Section B: hiring frozen.</p></div></div></div>""",
            "Section A: budget approved", "Section B: hiring frozen"),

        Single("Outlook signature inside a border-top div",
            """<div class="WordSection1"><p class="MsoNormal">Meeting moved to 3pm.</p><div style="border:none;border-top:solid windowtext 1.0pt;padding:1.0pt 0cm 0cm 0cm"><p class="MsoNormal">Dana Cohen | Operations</p></div></div>""",
            "Meeting moved to 3pm", "Dana Cohen | Operations"),

        Single("Hebrew memo header 'מאת:' without 'נשלח:'",
            """<div dir="rtl"><p>מאת: מחלקת כספים</p><p>אל: כל העובדים</p><p>-------------------</p><p>המשכורות יועברו ב-10 לחודש.</p></div>""",
            "מאת: מחלקת כספים", "המשכורות יועברו"),

        Single("Indented <blockquote> after a line ending with ':' (no address)",
            """<p>Please transfer to the following account:</p><blockquote><p>Bank: Leumi, Branch: 100</p><p>Account: 111111</p></blockquote><p>Thanks, Dana</p>""",
            "Account: 111111", "Thanks, Dana"),

        Single("Form fields after _____ line (From:, Consent:, Presented:)",
            """<div><p>Approval form</p><p>_______________</p><p>From: HR department</p><p>Consent: yes</p><p>Presented: 09/11/2026</p><p>Signature: Dana</p></div>""",
            "From: HR department", "Consent: yes", "Signature: Dana"),
    ];
}
