using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365EmailParser.Plugins
{
    /// <summary>
    /// Custom Action (implemented as a Dataverse Custom API — the modern, fully
    /// solution-portable form of a code-based custom action): given an email id, returns
    /// ONLY the latest message of a concatenated email thread, together with a version of
    /// that text in which every embedded attachment is replaced, at its original location,
    /// by a placeholder token "[[ATTACHMENT:&lt;guid&gt;]]". Downstream you fetch each
    /// attachment's Base64 (activitymimeattachment.body), OCR it (e.g. Azure Document
    /// Intelligence) and swap the placeholder for the extracted text — preserving reading order.
    ///
    /// This class is the Custom API's MAIN OPERATION plugin (bound via the Custom API's
    /// PluginTypeId — no manual SDK message step registration is required).
    ///
    ///   Custom API unique name : dlep_GetLatestEmailWithPlaceholders  (global / unbound)
    ///
    ///   REQUEST parameters (-> plugin InputParameters):
    ///     EmailId            (String, required)  GUID of the email (activityid).
    ///     PlaceholderFormat  (String, optional)  e.g. "[[ATTACHMENT:{0}]]" (default).
    ///
    ///   RESPONSE properties (-> plugin OutputParameters):
    ///     LatestText                  (String)  latest message, plain text.
    ///     LatestTextWithPlaceholders  (String)  latest text with attachment tokens inline.
    ///     LatestHtml                  (String)  latest message HTML (history trimmed).
    ///     AttachmentIds               (String)  JSON array of the latest-message GUIDs, in order.
    ///
    /// Invoke it exactly like an unbound action, e.g.:
    ///   POST [org]/api/data/v9.2/dlep_GetLatestEmailWithPlaceholders
    ///
    /// Sandbox (isolation) is supported because HtmlAgilityPack is ILMerged/ILRepack'd into
    /// this single assembly (see build steps in the repository README).
    /// </summary>
    public sealed class GetLatestEmailWithPlaceholders : IPlugin
    {
        // The Custom API unique name this plugin backs (informational; the platform wires the
        // main operation via the Custom API's PluginTypeId, so the message name is not read here).
        private const string CustomApiName = "dlep_GetLatestEmailWithPlaceholders";

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.UserId);

            try
            {
                // ---- resolve the email id (accept EmailId string, or a Target EntityReference) ----
                Guid emailId = ResolveEmailId(context);
                if (emailId == Guid.Empty)
                    throw new InvalidPluginExecutionException(
                        "GetLatestEmailWithPlaceholders: no email id supplied. Pass 'EmailId' (GUID) " +
                        "or bind the action to Email so 'Target' is available.");

                string placeholderFormat = GetInput(context, "PlaceholderFormat");
                if (string.IsNullOrWhiteSpace(placeholderFormat))
                    placeholderFormat = "[[ATTACHMENT:{0}]]";

                tracing.Trace("Retrieving email {0}", emailId);

                var email = service.Retrieve("email", emailId, new ColumnSet("description"));
                string html = email.GetAttributeValue<string>("description") ?? string.Empty;

                // ---- run the verified parser ----
                string latestHtml = EmailThreadParser.ExtractLatestMessageHtml(html);
                string latestText = EmailThreadParser.ExtractLatestMessageText(html);
                string placed = EmailThreadParser.ExtractLatestMessageTextWithPlaceholders(html, placeholderFormat);
                List<EmbeddedAttachment> atts = EmailThreadParser.ExtractLatestMessageAttachments(html);

                tracing.Trace("Latest message extracted; {0} embedded attachment(s).", atts.Count);

                // ---- outputs ----
                context.OutputParameters["LatestText"] = latestText;
                context.OutputParameters["LatestTextWithPlaceholders"] = placed;
                context.OutputParameters["LatestHtml"] = latestHtml;
                context.OutputParameters["AttachmentIds"] = ToJsonArray(atts);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("GetLatestEmailWithPlaceholders failed: {0}", ex);
                throw new InvalidPluginExecutionException(
                    "GetLatestEmailWithPlaceholders failed: " + ex.Message, ex);
            }
        }

        private static Guid ResolveEmailId(IPluginExecutionContext context)
        {
            var raw = GetInput(context, "EmailId");
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var parsed))
                return parsed;

            if (context.InputParameters.TryGetValue("Target", out var target))
            {
                if (target is EntityReference er && er.LogicalName == "email")
                    return er.Id;
                if (target is Entity e && e.LogicalName == "email")
                    return e.Id;
            }
            return Guid.Empty;
        }

        private static string GetInput(IPluginExecutionContext context, string name)
            => context.InputParameters.TryGetValue(name, out var v) ? v as string : null;

        /// <summary>Ordered JSON array: [{"attachmentId":"..","contentId":"..","alt":"..","width":n,"height":n}].</summary>
        private static string ToJsonArray(IEnumerable<EmbeddedAttachment> items)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var a in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('{');
                sb.Append("\"attachmentId\":").Append(JsonString(a.AttachmentId)).Append(',');
                sb.Append("\"contentId\":").Append(JsonString(a.ContentId)).Append(',');
                sb.Append("\"alt\":").Append(JsonString(a.AltText)).Append(',');
                sb.Append("\"width\":").Append(a.Width?.ToString(CultureInfo.InvariantCulture) ?? "null").Append(',');
                sb.Append("\"height\":").Append(a.Height?.ToString(CultureInfo.InvariantCulture) ?? "null");
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string JsonString(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
