# Custom API: `dlep_GetLatestEmailWithPlaceholders`

A Dynamics 365 / Dataverse **Custom API** (an unbound message backed by an `IPlugin`) that,
given an **email id**, returns **only the latest message** of a concatenated email thread —
plus the **embedded attachment GUIDs that belong to that latest message only**.

- Assembly: `D365EmailParser.Plugins.dll` (HtmlAgilityPack merged in, strong-named)
- Plugin type: `D365EmailParser.Plugins.GetLatestEmailWithPlaceholders`
- Custom API unique name: `dlep_GetLatestEmailWithPlaceholders`
- Binding: **Global** (unbound), **Function = false** (called as an Action via POST)

> **Custom API, not a legacy Process Action.** A Custom API is the modern, code-first message
> definition (`customapi` + `customapirequestparameter` + `customapiresponseproperty` tables)
> bound directly to the plugin type — no separate "Action" process or manual SDK-message step.
> It is still invoked exactly like an unbound action (`POST .../dlep_GetLatestEmailWithPlaceholders`).

---

## 1. What it does

1. Reads the email's body from the `email.description` column.
2. Strips the quoted reply history using `EmailThreadParser` (Outlook `divRplyFwdMsg`, Gmail
   `gmail_quote`, CKEditor `<hr>` + `From:` headers, `-----Original Message-----`, blockquotes,
   multilingual `From:/Sent:` headers, …).
3. Collects the inline/embedded attachments (`<img data-attachment-id="GUID">`) that appear
   **only in the trimmed latest message** — older GUIDs from quoted history are ignored.
4. Produces a plain-text version of the latest message in which every embedded attachment is
   replaced, **at its original reading-order location**, by a placeholder token
   (default `[[ATTACHMENT:<guid>]]`).

### Why placeholders?

The typical downstream flow:

```
dlep_GetLatestEmailWithPlaceholders
      │  LatestTextWithPlaceholders = "… account details [[ATTACHMENT:aaaa…]] please confirm"
      │  AttachmentIds              = [{ "attachmentId": "aaaa…", … }]
      ▼
For each attachmentId:
      Retrieve activitymimeattachment (body = Base64, mimetype, filename)
      ▼
      Azure AI Document Intelligence (OCR / layout) → extracted text
      ▼
Replace "[[ATTACHMENT:aaaa…]]" in LatestTextWithPlaceholders with the extracted text
      ▼
Fully reconstructed, reading-order-correct plain text of the newest message.
```

> This Custom API deliberately **stops at returning the latest text + the GUIDs**. The OCR /
> reassembly step is left to your flow (Power Automate, a second plugin, or external code), so
> it stays useful even for scenarios that don't need OCR.

---

## 2. Input / Output parameters

### Request parameters

| Name | Type | Required | Description |
| --- | --- | --- | --- |
| `EmailId` | String | Yes | GUID of the email record (`activityid`). |
| `PlaceholderFormat` | String | No | .NET composite format for the token. Default `[[ATTACHMENT:{0}]]`; `{0}` is replaced by the attachment GUID. |

> The plugin also accepts a `Target` `EntityReference`/`Entity` of logical name `email` if you
> later re-bind the Custom API to the Email table instead of passing `EmailId`.

### Response properties

| Name | Type | Description |
| --- | --- | --- |
| `LatestText` | String | The newest message as plain text (history removed). |
| `LatestTextWithPlaceholders` | String | Same text, with each embedded attachment replaced inline by the placeholder token. |
| `LatestHtml` | String | The newest message as HTML (history removed). |
| `AttachmentIds` | String | JSON array of the latest-message embedded attachments, in document order. |

`AttachmentIds` JSON shape:

```json
[
  { "attachmentId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "contentId": "ii_latestimage",
    "alt": "image.png",
    "width": 313,
    "height": 89 }
]
```

`attachmentId` is the **`activitymimeattachmentid`** — use it to retrieve the file from the
`activitymimeattachment` table (`body` = Base64, plus `filename`, `mimetype`, `contentid`).

---

## 3. How to call it

### Web API (unbound action)

```http
POST [Organization URI]/api/data/v9.2/dlep_GetLatestEmailWithPlaceholders
Content-Type: application/json

{
  "EmailId": "5297e3ea-9aaa-f111-aaab-7ced8d95b8f6",
  "PlaceholderFormat": "[[ATTACHMENT:{0}]]"
}
```

Response (shape):

```json
{
  "LatestText": "Hi, Please process a transfer …",
  "LatestTextWithPlaceholders": "Hi, Please process a transfer of 2500 to the account shown here [[ATTACHMENT:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa]]",
  "LatestHtml": "<div dir=\"ltr\"> … </div>",
  "AttachmentIds": "[{\"attachmentId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"contentId\":\"ii_latestimage\",\"alt\":\"image.png\",\"width\":313,\"height\":89}]"
}
```

### From Power Automate

Use **Perform an unbound action** → `dlep_GetLatestEmailWithPlaceholders`, pass `EmailId`
(the email GUID). Then loop `AttachmentIds`, retrieve each `activitymimeattachment`, OCR the
Base64 `body`, and `replace()` the matching `[[ATTACHMENT:<guid>]]` token in
`LatestTextWithPlaceholders`.

---

## 4. Retrieving the embedded attachment bytes

For each GUID in `AttachmentIds`:

```http
GET [Organization URI]/api/data/v9.2/activitymimeattachments(<attachmentId>)?$select=filename,mimetype,body,contentid
```

`body` is the Base64-encoded file content. Feed it to Azure AI Document Intelligence (or any
OCR), take the extracted text, and substitute it for the placeholder token at the exact
location in `LatestTextWithPlaceholders`.

---

## 5. Customisation notes

- **Publisher prefix:** the Custom API unique name uses the `dlep` publisher prefix. If you
  repackage under a different publisher, rename the Custom API and (optionally) update the
  `CustomApiName` constant in `GetLatestEmailWithPlaceholders.cs`. The constant is documentation
  only — runtime behaviour is driven by the registered Custom API, so parameter **UniqueNames**
  (`EmailId`, `PlaceholderFormat`) and response **UniqueNames** (`LatestText`,
  `LatestTextWithPlaceholders`, `LatestHtml`, `AttachmentIds`) must match the plugin's
  `InputParameters` / `OutputParameters` keys (they do out of the box).
- **Placeholder token:** change per call via `PlaceholderFormat`, or change the default in the
  parser (`ExtractLatestMessageTextWithPlaceholders`).
- **Boundary tuning:** add or adjust markers in `EmailThreadParser.cs`
  (`FindStructuralBoundary` / `IsLikelyBoundary` / the text-separator regexes) if your org
  receives a client format not covered.
- **Body column:** the plugin reads `email.description`. If you store the working copy of the
  body elsewhere, change the `ColumnSet`/attribute name in the plugin.

---

## 6. Verified behaviour (real emails)

Tested against two real Dataverse emails (Hebrew Outlook thread + Gmail thread):

| Email | Result |
| --- | --- |
| Outlook reply chain | Returned only the newest message; `AttachmentIds` = the **2** signature images of the latest message; the quoted history's images were excluded. |
| Gmail reply chain (`gmail_quote`) | Returned only the newest message + its **1** inline image; the quoted `blockquote` reply (with its own signature images) was excluded. |

---

## 7. Troubleshooting

| Symptom | Likely cause / fix |
| --- | --- |
| `0x8004416c "Public assembly must have public key token"` at registration | Sandbox assemblies must be strong-named. Merge with `/keyfile:plugin.snk` (see INSTALLATION.md). |
| `System.IO.FileNotFoundException: HtmlAgilityPack` at runtime | You registered the un-merged `bin` DLL. Register `merged/D365EmailParser.Plugins.dll`. |
| Output properties empty | Request/response **UniqueNames** differ from the plugin's parameter keys (case-sensitive). |
| Older (quoted) images returned | The client format's boundary isn't recognised — add its marker in `EmailThreadParser.cs`. |
| Nothing stripped (whole thread returned) | Single-message email, or an unrecognised boundary. Verify with the console `SampleRunner`. |

---

See **[INSTALLATION.md](INSTALLATION.md)** for how to install (solution import or manual
build-and-register).
