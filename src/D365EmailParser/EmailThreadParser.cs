using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

/// <summary>
/// Represents an inline/embedded attachment referenced by an &lt;img&gt; (or other element)
/// inside a Dataverse email body. In Dataverse the image carries:
///   * data-attachment-id -> the activitymimeattachment record's Id (GUID) — use this to
///     retrieve the file from Dataverse (activitymimeattachmentid).
///   * src="cid:CONTENTID"  -> the MIME Content-Id, matching the attachment's "contentid" column.
/// </summary>
public sealed class EmbeddedAttachment
{
    /// <summary>data-attachment-id — the activitymimeattachmentid GUID in Dataverse.</summary>
    public string? AttachmentId { get; set; }
    /// <summary>Content-Id from src="cid:..." — matches the activitymimeattachment.contentid column.</summary>
    public string? ContentId { get; set; }
    /// <summary>Raw src attribute value.</summary>
    public string? Src { get; set; }
    /// <summary>alt text, if any.</summary>
    public string? AltText { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public override string ToString()
        => $"AttachmentId={AttachmentId ?? "-"}, ContentId={ContentId ?? "-"}, " +
           $"Alt={AltText ?? "-"}, {Width?.ToString() ?? "?"}x{Height?.ToString() ?? "?"}";
}

/// <summary>
/// Extracts only the most recent message from a concatenated Dataverse / Dynamics 365
/// email body (the "description" field), stripping the quoted reply history.
///
/// Dataverse stores in "description" whatever HTML the composing client produced, so a
/// robust parser must recognise the boundary markers of the clients that feed a D365 org:
///   * Outlook / Exchange server-side sync -> &lt;div id="divRplyFwdMsg"&gt; and the
///     From:/Sent:/To:/Subject: header block.
///   * D365 web email editor (CKEditor) reply -> &lt;hr&gt; divider followed by a
///     &lt;b&gt;From:&lt;/b&gt; ... header paragraph.
///   * Gmail -> &lt;div class="gmail_quote"&gt; / &lt;blockquote class="gmail_quote"&gt;.
///   * Generic -> quoted &lt;blockquote&gt; (type="cite" / "... wrote:" attribution),
///     "-----Original Message-----", and _____ / ----- rules followed by a From:/Sent: header.
///     Rules and indent-only blockquotes inside ONE message are NOT treated as history.
/// </summary>
public static class EmailThreadParser
{
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

    /// <summary>Returns the HTML of only the newest message in the thread.</summary>
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

    /// <summary>Returns the newest message as clean plain text.</summary>
    public static string ExtractLatestMessageText(string html)
    {
        var latestHtml = ExtractLatestMessageHtml(html);
        var doc = new HtmlDocument();
        doc.LoadHtml(latestHtml);
        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText ?? string.Empty);
        text = text.Replace("\u00A0", " ");                       // non-breaking spaces
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"(\r?\n\s*){3,}", "\n\n");    // collapse blank lines
        return text.Trim();
    }

    /// <summary>
    /// Returns the embedded attachment references (data-attachment-id GUIDs and cid Content-Ids)
    /// found ONLY in the newest message — quoted-history attachments are excluded because the
    /// history is trimmed first. Use the AttachmentId values to fetch activitymimeattachment
    /// records from Dataverse.
    /// </summary>
    public static List<EmbeddedAttachment> ExtractLatestMessageAttachments(string html)
        => ScanEmbeddedAttachments(ExtractLatestMessageHtml(html));

    /// <summary>
    /// Returns EVERY inline/embedded attachment reference found in the WHOLE body (latest message
    /// AND quoted history), without trimming. Use this to build the set of Content-Ids / attachment
    /// GUIDs that are actually <c>cid:</c>-embedded in the HTML, so you can tell an inline image
    /// (signature logo, embedded screenshot) apart from a real file attachment that is only linked
    /// to the email as an <c>activitymimeattachment</c> record and never referenced in the body.
    /// </summary>
    public static List<EmbeddedAttachment> ExtractAllEmbeddedAttachments(string html)
        => ScanEmbeddedAttachments(html);

    /// <summary>Scans a body fragment for elements carrying data-attachment-id or a cid: src.</summary>
    private static List<EmbeddedAttachment> ScanEmbeddedAttachments(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html ?? string.Empty);

        var result = new List<EmbeddedAttachment>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Any element carrying data-attachment-id or a cid: src is an embedded attachment ref.
        var nodes = doc.DocumentNode.SelectNodes(
            "//*[@data-attachment-id] | //img[starts-with(@src,'cid:')]");
        if (nodes == null)
            return result;

        foreach (var n in nodes.OrderBy(x => x.StreamPosition))
        {
            var attId = Trim(n.GetAttributeValue("data-attachment-id", null));
            var src = Trim(n.GetAttributeValue("src", null));
            string? cid = null;
            if (!string.IsNullOrEmpty(src) &&
                src.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
                cid = src.Substring(4);

            if (string.IsNullOrEmpty(attId) && string.IsNullOrEmpty(cid))
                continue;

            // De-dupe on the strongest key available (attachment-id, else cid).
            var key = attId ?? "cid:" + cid;
            if (!seen.Add(key))
                continue;

            result.Add(new EmbeddedAttachment
            {
                AttachmentId = attId,
                ContentId = cid,
                Src = src,
                AltText = Trim(n.GetAttributeValue("alt", null)),
                Width = ParseInt(n.GetAttributeValue("width", null)),
                Height = ParseInt(n.GetAttributeValue("height", null))
            });
        }
        return result;
    }

    /// <summary>
    /// Returns the newest message as plain text, but with each embedded attachment replaced,
    /// AT ITS ORIGINAL LOCATION, by a placeholder token carrying the attachment GUID
    /// (default "[[ATTACHMENT:&lt;guid&gt;]]"). Downstream you can fetch each attachment's Base64,
    /// OCR it, and substitute the token with the extracted text — keeping correct reading order.
    /// Quoted-history attachments are excluded because history is trimmed first.
    /// </summary>
    /// <param name="html">Raw Dataverse email body (description).</param>
    /// <param name="placeholderFormat">A format string where {0} is the attachment GUID
    /// (falls back to the cid when no data-attachment-id is present).</param>
    public static string ExtractLatestMessageTextWithPlaceholders(
        string html, string placeholderFormat = "[[ATTACHMENT:{0}]]")
    {
        var latestHtml = ExtractLatestMessageHtml(html);
        var doc = new HtmlDocument();
        doc.LoadHtml(latestHtml);

        var nodes = doc.DocumentNode.SelectNodes(
            "//*[@data-attachment-id] | //img[starts-with(@src,'cid:')]");
        if (nodes != null)
        {
            foreach (var n in nodes.ToList())
            {
                var attId = Trim(n.GetAttributeValue("data-attachment-id", null));
                var src = Trim(n.GetAttributeValue("src", null));
                string? cid = null;
                if (!string.IsNullOrEmpty(src) &&
                    src.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
                    cid = src.Substring(4);

                var id = attId ?? cid;
                if (string.IsNullOrEmpty(id))
                    continue;

                var token = string.Format(placeholderFormat, id);
                // surround with newlines so the token sits on its own line in the text output
                var textNode = doc.CreateTextNode("\n" + token + "\n");
                n.ParentNode.ReplaceChild(textNode, n);
            }
        }

        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
        var text = HtmlEntity.DeEntitize(body.InnerText ?? string.Empty);
        text = text.Replace("\u00A0", " ");
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"(\r?\n\s*){3,}", "\n\n");
        return text.Trim();
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static int? ParseInt(string? s)
        => int.TryParse(s, out var v) ? v : (int?)null;

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

    private static HtmlNode? FindStructuralBoundary(HtmlNode root)
    {
        var candidates = root.SelectNodes(
            ".//div[@id='divRplyFwdMsg']" +                          // Outlook / Exchange reply header
            " | .//div[contains(@class,'gmail_quote')]" +            // Gmail
            " | .//blockquote[contains(@class,'gmail_quote')]" +
            " | .//div[contains(@class,'OutlookMessageHeader')]" +   // Outlook message header
            " | .//div[contains(@id,'x_divRplyFwdMsg')]" +           // Outlook x_ prefixed (D365 sanitised)
            " | .//blockquote" +                                     // generic quoted block
            " | .//hr");                                             // divider (validated below)

        if (candidates == null)
            return null;

        return candidates
            .OrderBy(n => n.StreamPosition)
            .FirstOrDefault(IsLikelyBoundary);
    }

    private static bool IsLikelyBoundary(HtmlNode node)
    {
        var id = node.GetAttributeValue("id", "");
        var cls = node.GetAttributeValue("class", "");

        if (id == "divRplyFwdMsg" || id.Contains("divRplyFwdMsg")) return true;
        if (cls.Contains("gmail_quote") || cls.Contains("OutlookMessageHeader")) return true;
        if (node.Name.Equals("blockquote", StringComparison.OrdinalIgnoreCase))
            return IsQuotedBlockquote(node);

        // A bare <hr> is a boundary ONLY when a From:/Sent: header follows shortly after.
        // This is the classic D365 web-editor reply divider.
        if (node.Name.Equals("hr", StringComparison.OrdinalIgnoreCase))
            return QuotedHeaderRegex.IsMatch(FollowingText(node));
        return false;
    }

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

    private static HtmlNode? FindTextBoundary(HtmlNode root)
    {
        // 1) explicit text separators ("-----Original Message-----", "Forwarded message")
        // 2) a From:/Sent: header block sitting in its own element
        // 3) a typed rule line (_____ / -----) immediately followed by a From:/Sent: header
        var textNodes = root.SelectNodes(".//text()[normalize-space()]");
        if (textNodes == null) return null;

        foreach (var tn in textNodes)
        {
            var t = HtmlEntity.DeEntitize(tn.InnerText);
            if (TextSeparatorRegex.IsMatch(t) || QuotedHeaderRegex.IsMatch(t.TrimStart()) ||
                IsRuleBeforeQuotedHeader(tn, t))
                return CutPointFor(tn);
        }
        return null;
    }

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

    /// <summary>
    /// Removes the boundary node plus every sibling after it, at each ancestor level. The
    /// ancestors themselves are kept because they also contain the newest message.
    /// </summary>
    private static void RemoveNodeAndEverythingAfter(HtmlNode boundary)
    {
        var current = boundary;
        while (current?.ParentNode != null)
        {
            var parent = current.ParentNode;
            var sibling = current.NextSibling;
            while (sibling != null)
            {
                var next = sibling.NextSibling;
                sibling.Remove();
                sibling = next;
            }
            if (current == boundary)
                current.Remove();

            if (parent.Name.Equals("body", StringComparison.OrdinalIgnoreCase) ||
                parent == parent.OwnerDocument.DocumentNode)
                break;
            current = parent;
        }
    }
}
