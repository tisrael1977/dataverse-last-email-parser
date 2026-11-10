# Parser fix: single emails truncated at section separators

- **Component:** `EmailThreadParser` (file `src/D365EmailParser/EmailThreadParser.cs`, which the
  plugin compiles via a link in `D365EmailParser.Plugins.csproj`)
- **Public API / Custom API contract:** unchanged. No new parameters, outputs or dependencies.
- **Compatibility:** .NET Framework 4.6.2, C# 9, HtmlAgilityPack 1.11.x and 1.13.x (both tested).
- **Shipped in:** solution `DataverseLastEmailParser` **1.0.2.0** (plugin assembly identity
  unchanged: Version 1.0.0.0, PublicKeyToken 58f87c72a67f0f07).
- **Independent of** the hidden-content cleanup in [HIDDEN-CONTENT-CLEANUP.md](HIDDEN-CONTENT-CLEANUP.md):
  the steps below apply whether or not that change is present in your copy.

---

## 1. Symptom

A **single** email (not a reply/forward) whose sections are separated by a typed line was
returned **truncated**: only the first section came back.

Typical case: a payment request listing several beneficiaries, one block per beneficiary,
separated by a line:

```
Please make the following transfers:
Mr. A — Bank X, branch 100, account 111111 — 12,345
____________________________________________     <- treated as "start of reply history"
Mr. B — Bank X, branch 200, account 222222 — 23,456      (lost)
____________________________________________
Ms. C — Bank Y, branch 300, account 333333              (lost)
```

## 2. Root causes

| # | Where (old code) | Problem |
|---|---|---|
| 1 | `TextSeparatorRegex` contained `\|(_{5,})` | **Any** line of 5+ underscores was treated as the start of the quoted history, even inside one message. (On a Hebrew keyboard `_` is Shift + `-`, so a typed "dash line" is often actually underscores.) |
| 2 | `IsLikelyBoundary`: `if (node.Name == "blockquote") return true;` | **Every** `<blockquote>` was treated as quoted history. Outlook on the web, new Outlook and Gmail emit `<blockquote>` for the **Increase indent** button, so indented sections were cut. |
| 3 | `ClosestBlock` + `RemoveNodeAndEverythingAfter` | `ClosestBlock` returned the **outermost** block ancestor, and `RemoveNodeAndEverythingAfter` also deleted every **ancestor** of the cut point. When the separator sat inside a wrapper that also holds the newest message (Outlook's `<div class="WordSection1">`, Gmail nested forwards, any wrapper `<div>`), the whole body was deleted and the result was **empty**. |
| 4 | `QuotedHeaderRegex` | Only 8 languages; Hebrew (`מאת:` / `נשלח:`), Arabic and others were not recognised. |

> Note: a line made only of hyphens (`-----`) did **not** cut the message in the old code by
> itself. If you see truncation on such an email, the line is usually underscores, or the
> sections are indented (`<blockquote>`), or the separator is inside a wrapper (cause 3).

## 3. What changed (summary)

| Step | Member in `EmailThreadParser` | Change |
|---|---|---|
| 1 | Regex fields (top of class) | `_{5,}` removed from `TextSeparatorRegex`; new `RuleLineRegex`, `AttributionRegex`, `InlineTags`; `QuotedHeaderRegex` rebuilt: ~25 languages, bidi marks (RTL), full-width colon, and `Sent` must not be the end of a longer word (`Consent:`). Hebrew `-----הודעה מקורית-----` added. |
| 2 | `IsLikelyBoundary` | `<blockquote>` → delegates to new `IsQuotedBlockquote`. `<hr>` check uses new `FollowingText`. |
| 3 | **New** `FollowingText`, `IsQuotedBlockquote`, `IsAttribution`, `LineText` | A blockquote is history only with quote evidence (`type="cite"`, quote class, From:/Sent: inside, or a "… wrote:" attribution). |
| 4 | `FindTextBoundary` | A typed rule (`_____`, `-----`, `=====`, `*****`, `~~~~~`, `—————`) is a boundary **only when a From:/Sent: header follows it**. Uses `CutPointFor` instead of `ClosestBlock`. |
| 5 | `ClosestBlock` **removed**; **new** `IsRuleBeforeQuotedHeader`, `CutPointFor` | Cut point = largest ancestor that **starts with** the separator, never a wrapper that also contains the newest message. |
| 6 | `RemoveNodeAndEverythingAfter` | Removes the boundary node and everything after it, but **keeps the ancestors**. |

Unchanged: `ExtractLatestMessageHtml`, `ExtractLatestMessageText`,
`ExtractLatestMessageTextWithPlaceholders`, `ExtractLatestMessageAttachments`,
`ExtractAllEmbeddedAttachments`, `FindStructuralBoundary`, and the plugin class.

Line numbers below refer to the fixed file in this repository; locate the members by name in
your copy.

---

## 4. Step-by-step changes

### Step 1: Regex fields (top of `EmailThreadParser`, ~lines 49–88)

**Replace** the old `QuotedHeaderRegex` and `TextSeparatorRegex` declarations:

```csharp
// OLD - remove
// Header block that opens a quoted message. Multi-language (EN/ES/DE/NL/IT/FR/PT/RU).
private static readonly Regex QuotedHeaderRegex = new Regex(
    @"^\s*(From|De|Von|Van|Da|От)\s*:.*?" +
    @"(Sent|Enviado|Gesendet|Verzonden|Inviato|Envoyé|Enviada|Отправлено)\s*:",
    RegexOptions.IgnoreCase | RegexOptions.Singleline);

// Plain-text separators that introduce the previous message.
private static readonly Regex TextSeparatorRegex = new Regex(
    @"(-{2,}\s*Original Message\s*-{2,})" +      // -----Original Message-----
    @"|(-{2,}\s*Forwarded message\s*-{2,})" +    // ---------- Forwarded message ----------
    @"|(_{5,})",                                 // ____________________ underscore rule
    RegexOptions.IgnoreCase);
```

**with:**

```csharp
// Whitespace plus the bidi marks (LRM/RLM/embeddings) that RTL clients put around labels.
private const string Gap = @"[\s\u200E\u200F\u202A-\u202E]*";
private const string Colon = "[:：]";

// Outlook "From:" / "Sent:" header labels in the client UI languages seen in D365 orgs.
private const string FromLabels =
    @"From|De|De la|Von|Van|Da|Od|Från|Fra|Lähettäjä|Kimden|Feladó|От|Від|Από|מאת|من|差出人|发件人|寄件者|보낸\s*사람";
private const string SentLabels =
    @"Sent|Enviado|Enviado el|Enviada|Enviada em|Gesendet|Verzonden|Inviato|Envoyé|Wysłane|Wysłano|" +
    @"Odesláno|Skickat|Sendt|Lähetetty|Gönderildi|Gönderilme Tarihi|Elküldve|Trimis|Отправлено|" +
    @"Надіслано|Στάλθηκε|נשלח|تم الإرسال|أرسل|送信日時|发送时间|寄件日期|보낸\s*날짜";

// Header block that opens a quoted message (From: ... Sent: ...), multi-language. The Sent
// label must not be the tail of a longer word ("Consent:", "Absent:").
private static readonly Regex QuotedHeaderRegex = new Regex(
    "^" + Gap + "(" + FromLabels + ")" + Gap + Colon + ".*?" +
    @"(?<![\p{L}\p{M}])(" + SentLabels + ")" + Gap + Colon,
    RegexOptions.IgnoreCase | RegexOptions.Singleline);

// Plain-text separators that always introduce the previous message.
private static readonly Regex TextSeparatorRegex = new Regex(
    @"(-{2,}\s*Original Message\s*-{2,})" +      // -----Original Message-----
    @"|(-{2,}\s*Forwarded message\s*-{2,})" +    // ---------- Forwarded message ----------
    @"|(-{2,}\s*הודעה מקורית\s*-{2,})",          // -----הודעה מקורית----- (Hebrew Outlook)
    RegexOptions.IgnoreCase);

// A rule typed as text (_____ / ----- / =====). Senders also use these to separate sections
// of ONE message, so it is a boundary only when a From:/Sent: header follows it.
private static readonly Regex RuleLineRegex = new Regex(
    @"_{5,}|-{5,}|={5,}|\*{5,}|~{5,}|[\u2013\u2014\u2500]{5,}");

// Attribution line that introduces a quoted <blockquote> ("On Mon, ... Bob wrote:").
private static readonly Regex AttributionRegex = new Regex(
    @"(wrote|writes|schrieb|a écrit|escribió|escreveu|scrisse|ha scritto|schreef|napisał|napisała|" +
    @"napsal|skrev|kirjoitti|yazdı|írta|написал|написала|написав|כתב|כתבה|כתב/ה|كتب|写道|寫道)" +
    Gap + Colon + Gap + "$",
    RegexOptions.IgnoreCase);

private static readonly HashSet<string> InlineTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "a", "b", "strong", "i", "em", "u", "span", "font", "small", "big", "sub", "sup" };
```

> The source file must be saved as **UTF-8** (it contains Hebrew/Arabic/CJK literals).

### Step 2: `IsLikelyBoundary` (~line 294)

**Replace** the blockquote line:

```csharp
// OLD
if (node.Name.Equals("blockquote", StringComparison.OrdinalIgnoreCase)) return true;
```
```csharp
// NEW
if (node.Name.Equals("blockquote", StringComparison.OrdinalIgnoreCase))
    return IsQuotedBlockquote(node);
```

**Replace** the `<hr>` block:

```csharp
// OLD
if (node.Name.Equals("hr", StringComparison.OrdinalIgnoreCase))
{
    var following = node.SelectNodes("following::*[position()<=8]");
    if (following == null) return false;
    var text = HtmlEntity.DeEntitize(string.Concat(following.Select(n => n.InnerText)));
    return QuotedHeaderRegex.IsMatch(text.TrimStart());
}
```
```csharp
// NEW
if (node.Name.Equals("hr", StringComparison.OrdinalIgnoreCase))
    return QuotedHeaderRegex.IsMatch(FollowingText(node));
```

### Step 3: Add four new methods (after `IsLikelyBoundary`, ~lines 311–362)

```csharp
// The next few non-empty text nodes after a node, one per line (so "Bob" + "Sent:" from
// "Bob<br><b>Sent:</b>" stay separate words).
private static string FollowingText(HtmlNode node, string prefix = "")
{
    var following = node.SelectNodes("following::text()[normalize-space()][position()<=12]");
    var parts = following == null ? Enumerable.Empty<string>() : following.Select(n => n.InnerText);
    return HtmlEntity.DeEntitize(prefix + "\n" + string.Join("\n", parts));
}

// Outlook on the web / new Outlook and Gmail emit <blockquote> for the "Increase indent"
// button too, so a blockquote is a boundary only when it carries quote evidence.
private static bool IsQuotedBlockquote(HtmlNode node)
{
    if (node.GetAttributeValue("type", "").Equals("cite", StringComparison.OrdinalIgnoreCase) ||
        node.Attributes["cite"] != null)
        return true;                                             // Apple Mail / Thunderbird

    var marker = node.GetAttributeValue("class", "") + " " + node.GetAttributeValue("id", "");
    if (Regex.IsMatch(marker, "quote|cite|attribution", RegexOptions.IgnoreCase))
        return true;

    if (QuotedHeaderRegex.IsMatch(HtmlEntity.DeEntitize(node.InnerText)))
        return true;                                             // starts with From:/Sent:

    // "... wrote:" attribution just before the blockquote, or as its first line.
    var previous = node.SelectSingleNode("preceding::text()[normalize-space()][1]");
    var first = node.SelectSingleNode(".//text()[normalize-space()]");
    return (previous != null && IsAttribution(LineText(previous, node))) ||
           (first != null && IsAttribution(LineText(first, null)));
}

// An attribution ends with a colon and either names the "wrote" verb or carries an address
// ("... <bob@x.com> כתב/ה:"), which an ordinary sentence before an indented list does not.
private static bool IsAttribution(string text)
{
    var t = HtmlEntity.DeEntitize(text).Trim().Trim('\u200E', '\u200F', '\u202A', '\u202B', '\u202C', '\u202D', '\u202E').Trim();
    if (!(t.EndsWith(":") || t.EndsWith("：")))
        return false;
    return AttributionRegex.IsMatch(t) || t.Contains("@");
}

// Text of the line (closest non-inline element) holding a text node; falls back to the text
// node itself when that element also contains the blockquote being evaluated.
private static string LineText(HtmlNode textNode, HtmlNode? blockquote)
{
    var line = textNode.ParentNode;
    while (line != null && InlineTags.Contains(line.Name))
        line = line.ParentNode;
    if (line == null || (blockquote != null && blockquote.Ancestors().Contains(line)))
        return textNode.InnerText;
    return line.InnerText;
}
```

### Step 4: `FindTextBoundary` (~line 364)

```csharp
// OLD (inside the foreach)
var t = HtmlEntity.DeEntitize(tn.InnerText);
if (TextSeparatorRegex.IsMatch(t) || QuotedHeaderRegex.IsMatch(t.TrimStart()))
{
    // climb to the closest block-level ancestor so we cut the whole header block
    var block = ClosestBlock(tn);
    return block ?? tn;
}
```
```csharp
// NEW
var t = HtmlEntity.DeEntitize(tn.InnerText);
if (TextSeparatorRegex.IsMatch(t) || QuotedHeaderRegex.IsMatch(t.TrimStart()) ||
    IsRuleBeforeQuotedHeader(tn, t))
    return CutPointFor(tn);
```

### Step 5: Delete `ClosestBlock`, add `IsRuleBeforeQuotedHeader` + `CutPointFor` (~lines 382–406)

**Delete** the whole `ClosestBlock(HtmlNode node)` method, and **add**:

```csharp
private static bool IsRuleBeforeQuotedHeader(HtmlNode textNode, string text)
{
    var rule = RuleLineRegex.Match(text);
    if (!rule.Success) return false;

    return QuotedHeaderRegex.IsMatch(FollowingText(textNode, text.Substring(rule.Index + rule.Length)));
}

// Climbs from the matched text to the largest ancestor that STARTS with it, so the whole
// header block is cut but never a wrapper (e.g. Outlook's <div class="WordSection1">)
// that also holds the newest message above the separator.
private static HtmlNode CutPointFor(HtmlNode textNode)
{
    var cut = textNode;
    var parent = textNode.ParentNode;
    while (parent != null &&
           !parent.Name.Equals("body", StringComparison.OrdinalIgnoreCase) &&
           parent != parent.OwnerDocument.DocumentNode &&
           parent.SelectSingleNode("(.//text()[normalize-space()] | .//img)[1]") == textNode)
    {
        cut = parent;
        parent = parent.ParentNode;
    }
    return cut;
}
```

### Step 6: `RemoveNodeAndEverythingAfter` (~line 412)

One line changes: only the boundary itself is removed; ancestors are kept.

```csharp
// OLD
current.Remove();
```
```csharp
// NEW
if (current == boundary)
    current.Remove();
```

### Notes for porting

- Required `using`s are unchanged: `System`, `System.Collections.Generic`, `System.Linq`,
  `System.Text.RegularExpressions`, `HtmlAgilityPack`.
- If your project does not enable nullable reference types, `HtmlNode?` only produces warning
  CS8632; you can drop the `?`.
- Language label lists (`FromLabels`, `SentLabels`, `AttributionRegex`) are plain alternations:
  add your users' Outlook UI language there if it is missing.

---

## 5. Behaviour before / after

| Input | Before | After |
|---|---|---|
| Single message, sections split by `_____` | First section only | Whole message |
| Single message, sections split by `-----` / `=====` / `*****` / `<hr>` | Whole message | Whole message (unchanged) |
| Single message with **Increase indent** (`<blockquote>`) | Cut at the indent | Whole message |
| Signature / disclaimer / fill-in form with `_____` | Cut | Kept |
| Separator or header inside a wrapper `<div>` (Outlook `WordSection1`, Gmail forward) | **Empty** result | Newest message only |
| Reply: `_____` rule + `From:`/`Sent:` (any of ~25 languages incl. Hebrew/Arabic) | Cut (EN only, often empty) | Cut correctly |
| Reply: Outlook web/mobile, Gmail, Apple Mail, Thunderbird, Zimbra, ProtonMail, `-----Original Message-----` | Cut | Cut (unchanged or now correct where it was empty) |
| Hebrew `-----הודעה מקורית-----`, D365 editor reply with `מאת:` / `נשלח:` | Whole thread | Newest message only |

## 6. Verification performed

A regression corpus of **105 emails** (38 single messages, 67 threads: Outlook desktop/web/
mobile, D365 editor, Gmail, Apple Mail, Thunderbird, Yahoo, ProtonMail, Zimbra, Outlook Express,
Samsung, helpdesk tools, plain text; ~25 languages incl. Hebrew and Arabic) was run through
**both** the old and the new parser (HtmlAgilityPack 1.11.62, the plugin's version):

| Result | Cases |
|---|---|
| Pass with old and new | 63 |
| Failed with old, pass with new | 37 |
| **Passed with old, fail with new (regressions)** | **0** |
| Fail with both (pre-existing limitations, §8) | 5 |

The corpus is in `src/D365EmailParser/EmailTestCorpus.cs` and
`EmailTestCorpus.Threads.cs`; run it with `dotnet run` in `src/D365EmailParser`. Exit code 1
means a failure.

## 7. Minimal acceptance tests (for your own code)

Feed each body to `ExtractLatestMessageText` and check the result. With the old code, tests
1–4, 6 and 7 fail; test 5 guards behaviour that must stay unchanged.

| # | HTML body | Must contain | Must NOT contain |
|---|---|---|---|
| 1 | `<div class="WordSection1"><p>A: 100</p><p>_______________</p><p>B: 200</p></div>` | `A: 100`, `B: 200` | n/a |
| 2 | `<div><div>Steps:</div><blockquote style="margin:0 0 0 40px;border:none"><div>2. Send ID</div></blockquote><div>Thanks</div></div>` | `2. Send ID`, `Thanks` | n/a |
| 3 | `<div class="WordSection1"><p>Paid.</p><p>________________________________</p><p><b>From:</b> Bob<br><b>Sent:</b> Monday</p><p>Please pay.</p></div>` | `Paid.` | `Please pay.` |
| 4 | `<div dir="rtl"><p>בוצע.</p><p>________________________________</p><p><b>מאת:</b> דני<br><b>נשלח:</b> יום שני</p><p>נא לבצע.</p></div>` | `בוצע.` | `נא לבצע.` |
| 5 | `<p>Done.</p><p>On Mon, Bob wrote:</p><blockquote><p>Is it done?</p></blockquote>` | `Done.` | `Is it done?` |
| 6 | `<div><p>OK.</p><p>-----Original Message-----<br>From: C<br>Sent: Mon</p><p>Review it.</p></div>` | `OK.` | `Review it.` |
| 7 | `<div><p>Form</p><p>_______</p><p>From: HR</p><p>Consent: yes</p></div>` | `From: HR`, `Consent: yes` | n/a |

## 8. Known limitations (not changed by this fix)

These formats returned the whole thread before the fix and still do. The fix does not make
them worse.

| Format | Effort to support | How |
|---|---|---|
| Outlook desktop (Win32) reply: `border-top` div, `<b>From:</b>` and `<b>Sent:</b>` in separate elements, no `<hr>`/rule | **Easy** (1 line, prototyped: 0 regressions on the corpus) | In `FindTextBoundary` replace `QuotedHeaderRegex.IsMatch(t.TrimStart())` with `QuotedHeaderRegex.IsMatch(FollowingText(tn, t))` so the header can span several elements; `CutPointFor` already climbs to the header div. |
| Same in Hebrew (`מאת:` / `נשלח:`) | **Easy** | Fixed by the same line (labels already supported). |
| Yahoo Mail (current) `div.yahoo_quoted`, no blockquote | **Easy** (2 lines, prototyped) | Add `" \| .//div[contains(@class,'yahoo_quoted')]"` to `FindStructuralBoundary` and `\|\| cls.Contains("yahoo_quoted")` to the class check in `IsLikelyBoundary`. |
| Outlook for Mac (`From:` / `Date:` / `Subject:`, no `Sent:`) | **Easy–medium** | Needs a second header pattern requiring From + Date + **Subject** together. Adding `Date` to `SentLabels` alone would cut single messages such as travel requests (`From: TLV / To: LHR / Date:`). |
| Plain-text body (no HTML tags, whole thread in one text node) | **Medium** (~25 lines) | The cut must **split** the text node at the separator position instead of removing the node (today the result is empty), and every rule in the node must be checked. |
