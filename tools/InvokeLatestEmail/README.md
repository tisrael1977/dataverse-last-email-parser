# InvokeLatestEmail — test console (.exe)

A tiny console app that **signs you in interactively (browser)** and calls the
`dlep_GetLatestEmailWithPlaceholders` Custom API for one email id, printing all five outputs.
Great for testing from VS Code without Postman or Power Automate.

## Run from source (VS Code terminal)

```bash
cd tools/InvokeLatestEmail

dotnet run -- https://tomer-israel-zini.crm4.dynamics.com 5297e3ea-9aaa-f111-aaab-7ced8d95b8f6
```

A browser window opens for sign-in the first time. After that you'll see `LatestText`,
`LatestTextWithPlaceholders`, `LatestHtml`, `AttachmentIds` (inline / `cid:`-embedded images
like signature logos), and `RegularAttachmentIds` (real file attachments — PDF, transfer image —
that are **not** `cid:`-embedded in the body).

Optional 3rd argument overrides the placeholder token:

```bash
dotnet run -- https://YOURORG.crm.dynamics.com <email-guid> "<<IMG:{0}>>"
```

## Build a standalone .exe

Framework-dependent (needs the .NET 9 runtime on the machine):

```bash
dotnet build -c Release
# → bin/Release/net9.0/InvokeLatestEmail.exe
bin\Release\net9.0\InvokeLatestEmail.exe https://YOURORG.crm.dynamics.com <email-guid>
```

Self-contained single file (no runtime needed, ~70 MB):

```bash
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -o publish
publish\InvokeLatestEmail.exe https://YOURORG.crm.dynamics.com <email-guid>
```

## Notes

- Interactive sign-in uses Microsoft's Azure CLI public application id
  (`04b07795-8ddb-461a-bbee-02f9e1bf7b46`) — no app registration required. For unattended /
  service scenarios, switch the connection string in `Program.cs` to
  `AuthType=ClientSecret;ClientId=…;ClientSecret=…`.
- Requires that the **Dataverse Last Email Parser** solution (the Custom API) is already
  installed in the target environment — see [../../docs/INSTALLATION.md](../../docs/INSTALLATION.md).
