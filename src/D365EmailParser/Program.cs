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
}
