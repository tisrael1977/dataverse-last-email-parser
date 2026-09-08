using System.Text;
using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

// InvokeLatestEmail — signs in interactively (browser, with device-code fallback)
// and calls the dlep_GetLatestEmailWithPlaceholders Custom API for one email id.
//
// Usage:
//   InvokeLatestEmail <orgUrl> <emailId> [placeholderFormat]
//
// Example:
//   InvokeLatestEmail https://tomer-israel-zini.crm4.dynamics.com 5297e3ea-9aaa-f111-aaab-7ced8d95b8f6

Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: InvokeLatestEmail <orgUrl> <emailId> [placeholderFormat]");
    Console.Error.WriteLine("Example: InvokeLatestEmail https://YOURORG.crm.dynamics.com <email-guid>");
    return 1;
}

string orgUrl = args[0].TrimEnd('/');
string emailId = args[1];
string? placeholderFormat = args.Length >= 3 ? args[2] : null;
const string ApiName = "dlep_GetLatestEmailWithPlaceholders";

// Public client used for interactive sign-in. This is the Azure CLI first-party app id,
// a public client registered with an http://localhost redirect, so the MSAL interactive
// (loopback browser) flow works without you registering an app of your own.
const string ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
string[] scopes = { $"{orgUrl}/.default" };

string token;
try
{
    var app = PublicClientApplicationBuilder
        .Create(ClientId)
        .WithAuthority(AzureCloudInstance.AzurePublic, "organizations")
        .WithRedirectUri("http://localhost")
        .Build();

    AuthenticationResult result;
    try
    {
        Console.WriteLine("Opening a browser for sign-in …");
        result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
    }
    catch (Exception exInteractive)
    {
        // Fallback: device code (works when a local browser can't be launched).
        Console.WriteLine("Interactive sign-in unavailable (" + exInteractive.Message + ").");
        Console.WriteLine("Falling back to device-code sign-in:\n");
        result = await app.AcquireTokenWithDeviceCode(scopes, dc =>
        {
            Console.WriteLine(dc.Message); // prints the URL + code to enter
            return Task.CompletedTask;
        }).ExecuteAsync();
    }

    token = result.AccessToken;
    Console.WriteLine($"Signed in as {result.Account?.Username}\n");
}
catch (Exception ex)
{
    Console.Error.WriteLine("Sign-in failed:");
    Console.Error.WriteLine(ex.ToString());
    return 2;
}

Console.WriteLine($"Calling {ApiName} for email {emailId} …\n");

try
{
    using var service = new ServiceClient(
        instanceUrl: new Uri(orgUrl),
        tokenProviderFunction: _ => Task.FromResult(token),
        useUniqueInstance: true,
        logger: null);

    if (!service.IsReady)
    {
        Console.Error.WriteLine("Failed to connect: " + service.LastError);
        if (service.LastException != null) Console.Error.WriteLine(service.LastException);
        return 3;
    }

    var request = new OrganizationRequest(ApiName) { ["EmailId"] = emailId };
    if (!string.IsNullOrEmpty(placeholderFormat))
        request["PlaceholderFormat"] = placeholderFormat;

    var response = service.Execute(request);

    string Get(string key) => response.Results.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";

    void Section(string title, string body)
    {
        Console.WriteLine(new string('=', 70));
        Console.WriteLine(title);
        Console.WriteLine(new string('-', 70));
        Console.WriteLine(body);
        Console.WriteLine();
    }

    Section("LatestText", Get("LatestText"));
    Section("LatestTextWithPlaceholders", Get("LatestTextWithPlaceholders"));
    Section("LatestHtml", Get("LatestHtml"));
    Section("AttachmentIds (JSON)", Get("AttachmentIds"));

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Custom API call failed:");
    Console.Error.WriteLine(ex.Message);
    return 4;
}
