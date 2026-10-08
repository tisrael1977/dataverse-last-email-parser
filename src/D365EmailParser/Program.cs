using System;

class Program
{
    static int _pass = 0, _fail = 0;

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "sample")
        {
            SampleRunner.Run(args);
            return;
        }

        // 1) Outlook / Exchange server-side sync (divRplyFwdMsg + From/Sent/To/Subject)
        Check("Outlook divRplyFwdMsg",
@"<html><body>
<div dir=""ltr"">
  <p>Thanks, that works for me. See you Monday.</p>
  <p>Regards,<br>Alice</p>
</div>
<div id=""divRplyFwdMsg"" dir=""ltr"">
  <hr>
  <b>From:</b> Bob &lt;bob@contoso.com&gt;<br>
  <b>Sent:</b> Friday, September 5, 2026 4:12 PM<br>
  <b>To:</b> Alice &lt;alice@contoso.com&gt;<br>
  <b>Subject:</b> Re: Project sync
  <div><p>Can we meet Monday?</p></div>
</div>
</body></html>",
            mustContain: "See you Monday",
            mustNotContain: "Can we meet Monday");

        // 2) D365 web email editor reply (<hr> divider + <b>From:</b> header paragraph)
        Check("D365 web editor hr divider",
@"<html><body>
<div>
  <p>Approved. Please proceed with the order.</p>
</div>
<div>
  <hr />
  <p><b>From:</b> Supplier &lt;sales@vendor.com&gt;<br>
     <b>Sent:</b> 04/09/2026 09:30<br>
     <b>To:</b> Buyer<br>
     <b>Subject:</b> Quote #4821</p>
  <p>Please confirm the attached quote.</p>
</div>
</body></html>",
            mustContain: "Approved",
            mustNotContain: "confirm the attached quote");

        // 3) Gmail sync (gmail_quote)
        Check("Gmail gmail_quote",
@"<html><body>
<div dir=""ltr"">Got it - shipping today.</div>
<div class=""gmail_quote"">
  <div dir=""ltr"" class=""gmail_attr"">On Fri, Sep 5, 2026 at 2:00 PM Sam &lt;sam@gmail.com&gt; wrote:</div>
  <blockquote class=""gmail_quote"">When will this ship?</blockquote>
</div>
</body></html>",
            mustContain: "shipping today",
            mustNotContain: "When will this ship");

        // 4) Plain-text separator (-----Original Message-----)
        Check("Original Message separator",
@"<html><body>
<div><p>No changes needed on our side.</p></div>
<div>-----Original Message-----<br>
From: Carol<br>Sent: yesterday<br>Subject: Review<br>
Please review the doc.</div>
</body></html>",
            mustContain: "No changes needed",
            mustNotContain: "Please review the doc");

        // 5) Header block without <hr> (From:/Sent: in its own paragraph)
        Check("Bare From/Sent header block",
@"<html><body>
<p>Confirmed for 10am.</p>
<p>From: Dave &lt;dave@x.com&gt; Sent: Monday To: Me Subject: Slot</p>
<p>Are you free at 10am?</p>
</body></html>",
            mustContain: "Confirmed for 10am",
            mustNotContain: "Are you free at 10am");

        // 6) No history at all - must be returned untouched
        Check("Single message (no history)",
@"<html><body><div><p>Hi team, kicking off the project today.</p></div></body></html>",
            mustContain: "kicking off the project",
            mustNotContain: "ZZZ_NOTHING_ZZZ");

        // 7) Gmail forward with hidden preheader and Proofpoint external-sender banner
        CheckExcluded("Gmail forward with injected chrome",
@"<html><body>
<div style=""display:none !important;visibility:hidden;mso-hide:all"">
  Latest sender ---------- Forwarded message --------- From: Older sender
</div>
<div style=""display:none !important"">ZjQcmQRYFpfptBannerStart</div>
<div id=""pfptBannert7408fg"">
  <div id=""pfptBannert7408fg"">External sender warning</div>
</div>
<div style=""display:none !important"">ZjQcmQRYFpfptBannerEnd</div>
<div dir=""auto""><div data-smartmail=""gmail_signature"">Latest sender</div></div>
<div class=""gmail_quote gmail_quote_container"">
  <div class=""gmail_attr"">---------- Forwarded message ---------</div>
  <div>Older message body</div>
</div>
</body></html>",
            mustContain: "Latest sender",
            "Forwarded message",
            "Older sender",
            "External sender warning",
            "pfptBanner",
            "Older message body");

        // 8) Outlook reply: preserve current inline attachment, exclude quoted attachment
        CheckAttachmentScenario(
            "Outlook reply with inline attachments",
@"<html><body>
<div style=""font-family:Arial;color:#123456"">
  Reply body
  <img src=""cid:current-logo"" alt=""current.png""
       data-attachment-id=""11111111-1111-1111-1111-111111111111"">
</div>
<div id=""divRplyFwdMsg"">
  <b>From:</b> Previous Sender<br><b>Sent:</b> Yesterday<br>
  Previous reply
  <img src=""cid:old-logo"" alt=""old.png""
       data-attachment-id=""99999999-9999-9999-9999-999999999999"">
</div>
</body></html>",
            mustContain: "Reply body",
            mustNotContain: "Previous reply",
            latestAttachmentId: "11111111-1111-1111-1111-111111111111",
            historicalAttachmentId: "99999999-9999-9999-9999-999999999999");

        // 9) Reply-all: To/Cc history is removed without affecting the latest body
        CheckExcluded("Reply-all header",
@"<html><body>
<div style=""font-family:Calibri"">Reply all body for everyone</div>
<div><hr>
  <p><b>From:</b> Previous Sender<br>
     <b>Sent:</b> Yesterday<br>
     <b>To:</b> Recipient One; Recipient Two<br>
     <b>Cc:</b> Copied Recipient<br>
     <b>Subject:</b> Re: Group thread</p>
  <p>Previous group message</p>
</div>
</body></html>",
            mustContain: "Reply all body for everyone",
            "Previous Sender",
            "Recipient One",
            "Copied Recipient",
            "Previous group message");

        // 10) Gmail forward: preserve a current inline image and remove hidden/quoted images
        CheckAttachmentScenario(
            "Gmail forward with inline attachments",
@"<html><body>
<div style=""DISPLAY: none !IMPORTANT"">
  Hidden forwarded preheader
  <img src=""cid:hidden-tracker""
       data-attachment-id=""eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"">
</div>
<div>Forward note
  <img src=""CID:current-scan"" alt=""scan.png""
       data-attachment-id=""aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"">
</div>
<div class=""gmail_quote gmail_quote_container"">
  <div class=""gmail_attr"">---------- Forwarded message ---------</div>
  Forwarded body
  <img src=""cid:forwarded-image""
       data-attachment-id=""bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"">
</div>
</body></html>",
            mustContain: "Forward note",
            mustNotContain: "Forwarded body",
            latestAttachmentId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            historicalAttachmentId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            allOnlyAttachmentId: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        // 11) Visible styling must not be mistaken for hidden content
        CheckExactText("Visible styled single message",
@"<html><body>
<div style=""display:block;visibility:visible;color:red"">Visible styled body</div>
<div aria-hidden=""FALSE"">Visible aria body</div>
<div style=""display:none-block"">Unusual but visible value</div>
</body></html>",
            "Visible styled body\nVisible aria body\nUnusual but visible value");

        Console.WriteLine($"\n==== {_pass} passed, {_fail} failed ====");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }

    static void Check(string name, string html, string mustContain, string mustNotContain)
    {
        var text = EmailThreadParser.ExtractLatestMessageText(html);
        bool ok = text.Contains(mustContain, StringComparison.OrdinalIgnoreCase)
               && !text.Contains(mustNotContain, StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
        if (!ok)
        {
            Console.WriteLine($"      expected to contain: \"{mustContain}\"");
            Console.WriteLine($"      expected to exclude: \"{mustNotContain}\"");
            Console.WriteLine($"      got: \"{text.Replace("\n", " ")}\"");
        }
        if (ok) _pass++; else _fail++;
    }

    static void CheckExcluded(
        string name, string html, string mustContain, params string[] mustNotContain)
    {
        var latestHtml = EmailThreadParser.ExtractLatestMessageHtml(html);
        var text = EmailThreadParser.ExtractLatestMessageText(html);
        var excludedValue = mustNotContain.FirstOrDefault(value =>
            latestHtml.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            text.Contains(value, StringComparison.OrdinalIgnoreCase));
        bool ok = text.Contains(mustContain, StringComparison.OrdinalIgnoreCase)
               && excludedValue == null;
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
        if (!ok)
        {
            Console.WriteLine($"      expected to contain: \"{mustContain}\"");
            if (excludedValue != null)
                Console.WriteLine($"      expected to exclude: \"{excludedValue}\"");
            Console.WriteLine($"      got HTML: \"{latestHtml.Replace("\n", " ")}\"");
            Console.WriteLine($"      got text: \"{text.Replace("\n", " ")}\"");
        }
        if (ok) _pass++; else _fail++;
    }

    static void CheckAttachmentScenario(
        string name,
        string html,
        string mustContain,
        string mustNotContain,
        string latestAttachmentId,
        string historicalAttachmentId,
        string? allOnlyAttachmentId = null)
    {
        var latestHtml = EmailThreadParser.ExtractLatestMessageHtml(html);
        var text = EmailThreadParser.ExtractLatestMessageText(html);
        var placed = EmailThreadParser.ExtractLatestMessageTextWithPlaceholders(html);
        var latest = EmailThreadParser.ExtractLatestMessageAttachments(html);
        var all = EmailThreadParser.ExtractAllEmbeddedAttachments(html);

        var expectedAllIds = new[] { latestAttachmentId, historicalAttachmentId, allOnlyAttachmentId }
            .Where(id => id != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actualAllIds = all
            .Select(attachment => attachment.AttachmentId)
            .Where(id => id != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool ok =
            latestHtml.Contains(mustContain, StringComparison.OrdinalIgnoreCase) &&
            !latestHtml.Contains(mustNotContain, StringComparison.OrdinalIgnoreCase) &&
            text.Contains(mustContain, StringComparison.OrdinalIgnoreCase) &&
            !text.Contains(mustNotContain, StringComparison.OrdinalIgnoreCase) &&
            placed.Contains($"[[ATTACHMENT:{latestAttachmentId}]]", StringComparison.OrdinalIgnoreCase) &&
            !placed.Contains(historicalAttachmentId, StringComparison.OrdinalIgnoreCase) &&
            (allOnlyAttachmentId == null ||
             !placed.Contains(allOnlyAttachmentId, StringComparison.OrdinalIgnoreCase)) &&
            latest.Count == 1 &&
            latest[0].AttachmentId?.Equals(
                latestAttachmentId, StringComparison.OrdinalIgnoreCase) == true &&
            actualAllIds.SetEquals(expectedAllIds);

        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
        if (!ok)
        {
            Console.WriteLine($"      latest HTML: \"{latestHtml.Replace("\n", " ")}\"");
            Console.WriteLine($"      latest text: \"{text.Replace("\n", " ")}\"");
            Console.WriteLine($"      placeholders: \"{placed.Replace("\n", " ")}\"");
            Console.WriteLine(
                $"      latest attachments: {string.Join(", ", latest.Select(a => a.AttachmentId))}");
            Console.WriteLine(
                $"      all attachments: {string.Join(", ", all.Select(a => a.AttachmentId))}");
        }
        if (ok) _pass++; else _fail++;
    }

    static void CheckExactText(string name, string html, string expected)
    {
        var actual = EmailThreadParser.ExtractLatestMessageText(html);
        var normalizedActual = actual.Replace("\r\n", "\n");
        var normalizedExpected = expected.Replace("\r\n", "\n");
        bool ok = normalizedActual == normalizedExpected;
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
        if (!ok)
        {
            Console.WriteLine($"      expected: \"{expected.Replace("\n", " ")}\"");
            Console.WriteLine($"      got: \"{actual.Replace("\n", " ")}\"");
        }
        if (ok) _pass++; else _fail++;
    }
}
