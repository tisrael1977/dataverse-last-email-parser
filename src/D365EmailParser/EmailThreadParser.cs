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
///   * Generic -> &lt;blockquote&gt;, "-----Original Message-----", long underscore rules.
/// </summary>
public static class EmailThreadParser
{
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

    /// <summary>Returns the HTML of only the newest message in the thread.</summary>
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
    {
        var latestHtml = ExtractLatestMessageHtml(html);
        var doc = new HtmlDocument();
        doc.LoadHtml(latestHtml);

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
        if (node.Name.Equals("blockquote", StringComparison.OrdinalIgnoreCase)) return true;

        // A bare <hr> is a boundary ONLY when a From:/Sent: header follows shortly after.
        // This is the classic D365 web-editor reply divider.
        if (node.Name.Equals("hr", StringComparison.OrdinalIgnoreCase))
        {
            var following = node.SelectNodes("following::*[position()<=8]");
            if (following == null) return false;
            var text = HtmlEntity.DeEntitize(string.Concat(following.Select(n => n.InnerText)));
            return QuotedHeaderRegex.IsMatch(text.TrimStart());
        }
        return false;
    }

    private static HtmlNode? FindTextBoundary(HtmlNode root)
    {
        // 1) explicit text separators ("-----Original Message-----", underscore rule)
        // 2) a From:/Sent: header block sitting in its own element
        var textNodes = root.SelectNodes(".//text()[normalize-space()]");
        if (textNodes == null) return null;

        foreach (var tn in textNodes)
        {
            var t = HtmlEntity.DeEntitize(tn.InnerText);
            if (TextSeparatorRegex.IsMatch(t) || QuotedHeaderRegex.IsMatch(t.TrimStart()))
            {
                // climb to the closest block-level ancestor so we cut the whole header block
                var block = ClosestBlock(tn);
                return block ?? tn;
            }
        }
        return null;
    }

    private static HtmlNode? ClosestBlock(HtmlNode node)
    {
        string[] blocks = { "p", "div", "table", "blockquote", "td", "li", "span" };
        var current = node.ParentNode;
        HtmlNode? best = null;
        while (current != null && !current.Name.Equals("body", StringComparison.OrdinalIgnoreCase))
        {
            if (blocks.Contains(current.Name, StringComparer.OrdinalIgnoreCase))
                best = current;
            current = current.ParentNode;
        }
        return best;
    }

    /// <summary>Removes the boundary node plus every sibling after it, at each ancestor level.</summary>
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
            current.Remove();

            if (parent.Name.Equals("body", StringComparison.OrdinalIgnoreCase) ||
                parent == parent.OwnerDocument.DocumentNode)
                break;
            current = parent;
        }
    }
}
