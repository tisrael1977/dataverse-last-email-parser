# Dataverse Last-Email Parser

Extract **only the latest message** from a concatenated Dynamics 365 / Dataverse email
thread (stripping the quoted reply history), together with the **embedded attachment GUIDs
that belong to that latest message only**.

Ships as:

1. A small, dependency-light **C# parser library** (`EmailThreadParser`) you can reuse
   anywhere.
2. A ready-to-register **Dataverse Custom API plugin**
   (`dlep_GetLatestEmailWithPlaceholders`) that takes an email id and returns the trimmed
   text/HTML plus the embedded attachment GUIDs.
3. Importable **managed and unmanaged solution** zips (in `solution/dist/`) so you can add
   the Custom API to any environment without building anything.

---

## Why

When an email in Dataverse is a long reply chain, the `description` (body) column holds the
**entire thread** — the new message *plus* every quoted older message. Most automation
(auto-response, sentiment, routing, OCR of inline images, LLM summarisation) only wants the
**newest** message. Naively feeding the whole thread produces wrong results and wastes tokens.

This parser recognises the boundary markers of the mail clients that feed a D365 org and
returns just the top message. It also returns the **inline/embedded attachment GUIDs**
(`data-attachment-id` = `activitymimeattachmentid`) found **in that latest message only** —
so you can pull the right images from Dataverse and ignore GUIDs that were part of older,
quoted messages.

---

## What it detects

Quoted-history boundaries handled (structural first, then text fallbacks):

| Client / format | Marker |
| --- | --- |
| Outlook / Exchange server-side sync | `<div id="divRplyFwdMsg">`, `From:/Sent:/To:/Subject:` header block |
| D365 web email editor (CKEditor) | `<hr>` divider followed by a `<b>From:</b> …` header |
| Gmail | `<div class="gmail_quote">` / `<blockquote class="gmail_quote">` |
| Generic | `<blockquote>`, `-----Original Message-----`, `---------- Forwarded message ----------`, long underscore rules |
| Multi-language headers | From/Sent in EN, ES, DE, NL, IT, FR, PT, RU |

A single message with no history is returned untouched.

---

## Parser API (`EmailThreadParser`)

```csharp
// The newest message as HTML (history removed).
string ExtractLatestMessageHtml(string html);

// The newest message as plain text.
string ExtractLatestMessageText(string html);

// Newest-message plain text with each embedded attachment replaced, in place,
// by a token (default "[[ATTACHMENT:{guid}]]") so you can re-insert OCR'd text later.
string ExtractLatestMessageTextWithPlaceholders(string html, string placeholderFormat = "[[ATTACHMENT:{0}]]");

// Embedded attachments (GUIDs) that belong ONLY to the newest message, in document order.
List<EmbeddedAttachment> ExtractLatestMessageAttachments(string html);
```

`EmbeddedAttachment` exposes `AttachmentId` (the `activitymimeattachmentid` GUID),
`ContentId` (from `src="cid:…"`, matching the `contentid` column), `Src`, `AltText`,
`Width`, `Height`.

---

## Repository layout

```
dataverse-last-email-parser/
├── README.md
├── LICENSE
├── docs/
│   ├── CUSTOM_ACTION.md          # What the Custom API does + the downstream OCR flow
│   └── INSTALLATION.md           # Install guide (solution import OR manual build/register)
├── samples/
│   ├── outlook-thread.html       # Sanitized Outlook (divRplyFwdMsg) fixture
│   └── gmail-thread.html         # Sanitized Gmail (gmail_quote) fixture
├── solution/
│   ├── dist/                     # Importable solution zips
│   │   ├── DataverseLastEmailParser_managed.zip
│   │   └── DataverseLastEmailParser_unmanaged.zip
│   └── deploy/deploy.ps1         # Idempotent Web API deployer (advanced/dev use)
└── src/
    ├── D365EmailParser/          # Parser library + console tests (net10.0)
    │   ├── EmailThreadParser.cs   # ← the reusable parser (shared with the plugin)
    │   ├── Program.cs             # 6 synthetic unit tests
    │   └── SampleRunner.cs        # generic "run on a file" demo
    └── D365EmailParser.Plugins/  # Dataverse Custom API plugin (net462)
        ├── GetLatestEmailWithPlaceholders.cs
        └── D365EmailParser.Plugins.csproj   # links the shared parser source
```

> The plugin project **links** `../D365EmailParser/EmailThreadParser.cs` — the exact same,
> tested parser code is compiled into both the console app and the plugin.

---

## Build & test (parser / console)

Requires the .NET SDK.

```bash
cd src/D365EmailParser

# Run the synthetic unit tests (6 formats):
dotnet run

# Run against a sample email file (prints all four outputs):
dotnet run -- sample ../../samples/outlook-thread.html
dotnet run -- sample ../../samples/gmail-thread.html
```

Expected on the samples: the latest message is kept, the quoted history is removed, and only
the **latest-message** attachment GUIDs are returned (the older `9999…` / `bbbb…` GUIDs from
the quoted history are excluded).

---

## Build the Dataverse plugin

The plugin targets **.NET Framework 4.6.2** and depends on **HtmlAgilityPack**, which must be
merged into the plugin assembly for sandbox (isolation) mode.

```bash
cd src/D365EmailParser.Plugins

# (optional) create your own strong-name key so the merged assembly is signed:
sn -k plugin.snk

dotnet build -c Release

# Merge HtmlAgilityPack into a single deployable assembly:
dotnet tool install --global dotnet-ilrepack
ilrepack /keyfile:plugin.snk /out:merged/D365EmailParser.Plugins.dll ^
         /lib:bin/Release ^
         bin/Release/D365EmailParser.Plugins.dll ^
         bin/Release/HtmlAgilityPack.dll
```

Register `merged/D365EmailParser.Plugins.dll` (the merged one — **not** the `bin` copy) in
Dataverse. Full step-by-step in **[docs/INSTALLATION.md](docs/INSTALLATION.md)**.

> `plugin.snk` is intentionally **git-ignored** — never commit a signing key. Sandbox
> (isolation-mode) assemblies **must be strong-named**, so supply your own key (`sn -k
> plugin.snk`) and pass `/keyfile:` to ILRepack for a release build.

---

## Install (fastest path: import the solution)

You do **not** need to build anything to use this. Import a zip from `solution/dist/`:

- **`DataverseLastEmailParser_managed.zip`** — for test/prod environments (locked, cleanly
  uninstallable).
- **`DataverseLastEmailParser_unmanaged.zip`** — for a dev environment you want to customize.

```bash
pac auth create --url https://YOURORG.crm.dynamics.com
pac solution import --path solution/dist/DataverseLastEmailParser_managed.zip --publish-changes
```

Or in the maker portal: **Solutions → Import solution → Browse** to the zip → **Next → Import**.

Full details and the manual (build-and-register) alternative are in
**[docs/INSTALLATION.md](docs/INSTALLATION.md)**.

---

## Custom API at a glance

`dlep_GetLatestEmailWithPlaceholders` (unbound / global, function=false)

**Input**

| Name | Type | Required | Notes |
| --- | --- | --- | --- |
| `EmailId` | String | Yes | GUID of the email (`activityid`). |
| `PlaceholderFormat` | String | No | Default `[[ATTACHMENT:{0}]]`. |

**Output**

| Name | Type | Description |
| --- | --- | --- |
| `LatestText` | String | Latest message, plain text. |
| `LatestTextWithPlaceholders` | String | Latest text with attachment tokens inline. |
| `LatestHtml` | String | Latest message HTML (history trimmed). |
| `AttachmentIds` | String | JSON array of the latest-message attachment GUIDs, in order. |

Invoke it like any unbound action:

```http
POST /api/data/v9.2/dlep_GetLatestEmailWithPlaceholders
Content-Type: application/json

{ "EmailId": "5297e3ea-9aaa-f111-aaab-7ced8d95b8f6" }
```

See **[docs/CUSTOM_ACTION.md](docs/CUSTOM_ACTION.md)** for the full description and the
downstream OCR / Document Intelligence flow.

---

## License

[MIT](LICENSE)
