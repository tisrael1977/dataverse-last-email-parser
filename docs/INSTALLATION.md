# Installation Guide

Two ways to install **`dlep_GetLatestEmailWithPlaceholders`** into a Dataverse / Dynamics 365
environment:

- **[Option A — Import the solution](#option-a--import-the-solution-recommended)** (no build; fastest).
- **[Option B — Build and register manually](#option-b--build-and-register-manually)** (for developers who want to customize the code).

---

## Prerequisites

- A Dataverse environment and an account with **System Administrator** or **System Customizer**.
- For Option A: [Power Platform CLI](https://learn.microsoft.com/power-platform/developer/cli/introduction)
  (`pac`) **or** access to the maker portal (make.powerapps.com).
- For Option B, additionally: the **.NET SDK**, **.NET Framework 4.6.2 targeting pack**,
  **ILRepack** (`dotnet tool install --global dotnet-ilrepack`), and a strong-name key tool
  (`sn.exe`, ships with the .NET Framework SDK / Visual Studio).

---

## Option A — Import the solution (recommended)

Two zips ship in **`solution/dist/`**:

| File | Use |
| --- | --- |
| `DataverseLastEmailParser_managed.zip` | Test / production. Locked, cleanly uninstallable. |
| `DataverseLastEmailParser_unmanaged.zip` | A dev environment you intend to customize. |

### A1. With the Power Platform CLI

```bash
# Authenticate to the target environment (opens a browser)
pac auth create --url https://YOURORG.crm.dynamics.com

# Import (managed shown; swap the filename for the unmanaged zip if desired)
pac solution import \
  --path solution/dist/DataverseLastEmailParser_managed.zip \
  --publish-changes
```

### A2. With the maker portal

1. Go to **make.powerapps.com** and select your environment (top-right).
2. **Solutions → Import solution → Browse**.
3. Choose the zip from `solution/dist/`.
4. **Next → Import** and wait for "Import succeeded".

### A3. Verify

The solution **Dataverse Last Email Parser** now contains:

- Plugin assembly `D365EmailParser.Plugins`
- Plugin type `D365EmailParser.Plugins.GetLatestEmailWithPlaceholders`
- Custom API `dlep_GetLatestEmailWithPlaceholders` with request params `EmailId`,
  `PlaceholderFormat` and response props `LatestText`, `LatestTextWithPlaceholders`,
  `LatestHtml`, `AttachmentIds`.

Test it (any REST client authenticated to the org):

```http
POST https://YOURORG.crm.dynamics.com/api/data/v9.2/dlep_GetLatestEmailWithPlaceholders
Content-Type: application/json

{ "EmailId": "<an-email-guid>" }
```

You should get back `LatestText`, `LatestTextWithPlaceholders`, `LatestHtml`, and
`AttachmentIds`.

### Uninstall

- **Managed:** delete the **Dataverse Last Email Parser** solution from **Solutions** — this
  removes the Custom API, plugin type, and assembly.
- **Unmanaged:** delete the individual components (or the solution), as usual for unmanaged
  content.

---

## Option B — Build and register manually

Use this if you changed the parser or plugin code and want to ship your own assembly.

### B1. Build and merge the plugin

The plugin targets **.NET Framework 4.6.2** and depends on **HtmlAgilityPack**, which must be
merged into a single assembly for sandbox (isolation) mode. Sandbox assemblies must also be
**strong-named**.

```bash
cd src/D365EmailParser.Plugins

# 1. Create a strong-name key (required for sandbox registration)
sn -k plugin.snk

# 2. Build Release
dotnet build -c Release

# 3. Merge HtmlAgilityPack in and sign with the key
dotnet tool install --global dotnet-ilrepack
ilrepack /keyfile:plugin.snk /out:merged/D365EmailParser.Plugins.dll ^
         /lib:bin/Release ^
         bin/Release/D365EmailParser.Plugins.dll ^
         bin/Release/HtmlAgilityPack.dll
```

Register **`merged/D365EmailParser.Plugins.dll`** — *not* the `bin` copy.

> `plugin.snk` is git-ignored on purpose; never commit a signing key.

### B2. Register the assembly + Custom API

**Easiest — the included script** (Web API, idempotent):

```powershell
# Get a bearer token for the org however you prefer, e.g. with Azure CLI:
$org = "https://YOURORG.crm.dynamics.com"
$tok = az account get-access-token --resource $org --query accessToken -o tsv

./solution/deploy/deploy.ps1 -OrgUrl $org -Token $tok `
    -DllPath ./src/D365EmailParser.Plugins/merged/D365EmailParser.Plugins.dll
```

The script creates (or reuses) the publisher + solution, then registers the assembly, plugin
type, Custom API, its 2 request parameters and 4 response properties.

**Or with the Plugin Registration Tool (PRT) + maker portal:**

1. `pac tool prt` to launch the Plugin Registration Tool; **Create Connection** to your env.
2. **Register → Register New Assembly** → select `merged/D365EmailParser.Plugins.dll`,
   Isolation **Sandbox**, Location **Database** → **Register Selected Plugins**.
3. In **make.powerapps.com**, create a **Custom API** (or import the unmanaged solution's
   Custom API component) with:
   - **Unique Name:** `dlep_GetLatestEmailWithPlaceholders`
   - **Binding Type:** Global — **Is Function:** No — **Allowed Custom Processing Step Type:** None
   - **Plugin Type:** `D365EmailParser.Plugins.GetLatestEmailWithPlaceholders`
   - **Request Parameters:** `EmailId` (String, Required), `PlaceholderFormat` (String, Optional)
   - **Response Properties:** `LatestText`, `LatestTextWithPlaceholders`, `LatestHtml`,
     `AttachmentIds` (all String)

   > Parameter/response **Unique Names are case-sensitive** and must match exactly.
4. **Publish all customizations.**

### B3. (Optional) Re-export your own solution

```bash
pac solution export --name DataverseLastEmailParser --managed false --path ./solution/dist/DataverseLastEmailParser_unmanaged.zip --overwrite
pac solution export --name DataverseLastEmailParser --managed true  --path ./solution/dist/DataverseLastEmailParser_managed.zip  --overwrite
```

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `0x8004416c "Public assembly must have public key token"` | Assembly isn't strong-named. Rebuild merging with `/keyfile:plugin.snk`. |
| `FileNotFoundException: HtmlAgilityPack` at runtime | You registered the `bin` DLL. Register the **merged** one. |
| Import fails: "publisher prefix mismatch" | Import into a clean environment, or delete a previous conflicting publisher/solution. |
| Custom API returns empty output | Request/response Unique Names don't match the plugin keys (case-sensitive). |
| `pac` not found | `dotnet tool install --global Microsoft.PowerApps.CLI.Tool` then reopen the shell. |

See **[CUSTOM_ACTION.md](CUSTOM_ACTION.md)** for what the API does and how to consume it.
