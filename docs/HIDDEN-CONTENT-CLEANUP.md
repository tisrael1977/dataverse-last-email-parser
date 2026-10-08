# Hidden Email Content Cleanup

This guide documents the changes required to remove hidden preheader text, injected
Proofpoint banners, and forwarded Gmail history from the latest-message output.

Use these instructions to apply the same change manually in another environment or
copy of the code.

## Result

For the example Gmail email, the previous `LatestText` included:

```text
ענבל אלון
---------- Forwarded message ---------
מאת: ענבל אלון <inbalal9@gmail.com>
Date: יום ג׳, 18 באוג׳ 2026, 17:21
Subject: חשבון 188660919
To: סניף מרכז הכרמל - צוות עסקי <076-business@dbank.co.il>
ענבל אלון

ZjQcmQRYFpfptBannerStart

האימייל הזה הוא משולח חיצוני
האימייל הזה הגיע מחוץ לארגון שלך.

ZjQcmQRYFpfptBannerEnd

ענבל אלון
```

After this change, `LatestText` and `LatestTextWithPlaceholders` return:

```text
ענבל אלון
```

## Production code changes

All production changes are in:

```text
src/D365EmailParser/EmailThreadParser.cs
```

The plugin project links this source file, so the same parser implementation is compiled
into both the console application and the Dataverse plugin.

### 1. Clean non-message content before finding the history boundary

Find `ExtractLatestMessageHtml`:

```csharp
public static string ExtractLatestMessageHtml(string html)
{
    if (string.IsNullOrWhiteSpace(html))
        return html ?? string.Empty;

    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    var root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;

    var boundary = FindStructuralBoundary(root) ?? FindTextBoundary(root);
    if (boundary != null)
        RemoveNodeAndEverythingAfter(boundary);

    var outBody = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
    return outBody.InnerHtml.Trim();
}
```

Insert this call immediately after `root` is assigned and before
`FindStructuralBoundary` is called:

```csharp
RemoveNonMessageContent(root);
```

The updated function must be:

```csharp
public static string ExtractLatestMessageHtml(string html)
{
    if (string.IsNullOrWhiteSpace(html))
        return html ?? string.Empty;

    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    var root = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
    RemoveNonMessageContent(root);

    var boundary = FindStructuralBoundary(root) ?? FindTextBoundary(root);
    if (boundary != null)
        RemoveNodeAndEverythingAfter(boundary);

    var outBody = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
    return outBody.InnerHtml.Trim();
}
```

Cleaning must happen before boundary detection. This prevents a forwarded-message marker
inside a hidden preheader from being treated as the real visible history boundary.

### 2. Add `RemoveNonMessageContent`

In the same `EmailThreadParser` class, add the following private function after
`ParseInt` and before `FindStructuralBoundary`:

```csharp
private static void RemoveNonMessageContent(HtmlNode root)
{
    foreach (var node in root.Descendants().OrderByDescending(n => n.StreamPosition).ToList())
    {
        if (node.ParentNode == null)
            continue;

        var style = node.GetAttributeValue("style", "");
        var isHiddenByStyle =
            HasCssPropertyValue(style, "display", "none") ||
            HasCssPropertyValue(style, "visibility", "hidden") ||
            HasCssPropertyValue(style, "mso-hide", "all");

        if (node.Name.Equals("script", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("style", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("noscript", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("template", StringComparison.OrdinalIgnoreCase) ||
            node.Attributes["hidden"] != null ||
            node.GetAttributeValue("aria-hidden", "").Equals("true", StringComparison.OrdinalIgnoreCase) ||
            node.GetAttributeValue("id", "").StartsWith("pfptBanner", StringComparison.OrdinalIgnoreCase) ||
            isHiddenByStyle)
        {
            node.Remove();
        }
    }
}

private static bool HasCssPropertyValue(string style, string property, string value)
    => Regex.IsMatch(
        style,
        $@"(?:^|;)\s*{Regex.Escape(property)}\s*:\s*{Regex.Escape(value)}\s*" +
        @"(?:!important\s*)?(?:;|$)",
        RegexOptions.IgnoreCase);
```

This function removes:

- `<script>`, `<style>`, `<noscript>`, and `<template>` content.
- Elements with the HTML `hidden` attribute.
- Elements with `aria-hidden="true"`.
- Elements hidden by `display:none`.
- Elements hidden by `visibility:hidden`.
- Elements hidden by the Outlook-specific `mso-hide:all`.
- Proofpoint banner elements with IDs beginning with `pfptBanner`.

Nodes are processed in reverse document order so a child can be removed safely before
its parent. The `ParentNode` check skips a child that was already detached when its
parent was removed.

`HasCssPropertyValue` requires a complete CSS property value, with optional
`!important`. It does not incorrectly treat visible values such as
`display:none-block` as `display:none`, and matching is case-insensitive.

## Why all parser outputs are affected

The following functions already call `ExtractLatestMessageHtml`, directly or indirectly:

```csharp
ExtractLatestMessageText
ExtractLatestMessageTextWithPlaceholders
ExtractLatestMessageAttachments
```

Therefore, no separate cleanup call is required in those functions. Adding cleanup in
`ExtractLatestMessageHtml` keeps these outputs consistent:

- `LatestHtml`
- `LatestText`
- `LatestTextWithPlaceholders`
- Latest-message embedded attachment references

`ExtractAllEmbeddedAttachments` intentionally scans the complete original email and is
not changed.

## Optional regression test

The local synthetic tests are in:

```text
src/D365EmailParser/Program.cs
```

Add this test after the existing single-message test:

```csharp
// Gmail forward with hidden preheader and Proofpoint external-sender banner
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
```

Add this test helper inside the `Program` class:

```csharp
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
```

The project has implicit global usings enabled, so no additional `using` directive is
required for `FirstOrDefault`.

## Validation

From the repository root, run:

```powershell
dotnet run --project .\src\D365EmailParser\D365EmailParser.csproj
```

The current regression suite covers Outlook reply, D365 reply-all, Gmail reply and
forward, plain-text boundaries, single messages, hidden Proofpoint content, current and
historical inline attachments, placeholder placement, complete-body attachment
discovery, and preservation of visible styled content.

Expected summary:

```text
==== 11 passed, 0 failed ====
```

Regular file attachments such as PDFs are not represented by elements in the HTML and
are not modified by this cleanup. The plugin's regular-attachment classification uses
`ExtractAllEmbeddedAttachments(fullHtml)` against the original, untrimmed HTML. The
attachment regression cases verify that this complete-body scan still returns current,
hidden, and historical embedded references while latest-message APIs return only the
visible attachment in the newest message.

## Files that do not need changes

Do not change:

```text
tools/InvokeLatestEmail/Program.cs
```

Its selected `return 0;` only reports that the Custom API invocation completed
successfully. It does not participate in parsing or cleaning the email body.

No changes are required in:

```text
src/D365EmailParser.Plugins/GetLatestEmailWithPlaceholders.cs
```

The plugin already calls the shared `EmailThreadParser` functions.
