using System.Text;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

// InvokeLatestEmail — signs in interactively (browser) and calls the
// dlep_GetLatestEmailWithPlaceholders Custom API for one email id.
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

// Public "sample" application id shipped by Microsoft for interactive Dataverse sign-in.
const string SampleAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
const string SampleRedirect = "app://58145B91-0C36-4500-8554-080854F2AC97";

var connectionString =
    $"AuthType=OAuth;Url={orgUrl};AppId={SampleAppId};RedirectUri={SampleRedirect};LoginPrompt=Auto;RequireNewInstance=True";

Console.WriteLine($"Connecting to {orgUrl} … a browser window will open for sign-in.");

using var service = new ServiceClient(connectionString);
if (!service.IsReady)
{
    Console.Error.WriteLine("Failed to connect: " + service.LastError);
    if (service.LastException != null) Console.Error.WriteLine(service.LastException);
    return 2;
}

Console.WriteLine($"Connected as {service.OAuthUserId ?? service.ConnectedOrgFriendlyName}");
Console.WriteLine($"Calling {ApiName} for email {emailId} …\n");

var request = new OrganizationRequest(ApiName)
{
    ["EmailId"] = emailId
};
if (!string.IsNullOrEmpty(placeholderFormat))
    request["PlaceholderFormat"] = placeholderFormat;

try
{
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
    return 3;
}
