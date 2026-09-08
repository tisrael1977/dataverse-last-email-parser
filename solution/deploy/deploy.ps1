<#
  Deploys the D365 Email Parser plugin + Custom API into a Dataverse environment via the Web API,
  and adds all components to a solution so it can be exported (managed & unmanaged).

  Prereq: an OAuth access token for the target org (Dataverse resource).
  Usage:
    $tok = az account get-access-token --resource https://<org>.crm.dynamics.com --query accessToken -o tsv
    ./deploy.ps1 -OrgUrl "https://<org>.crm.dynamics.com" -Token $tok -DllPath ..\..\src\D365EmailParser.Plugins\merged\D365EmailParser.Plugins.dll
#>
param(
  [Parameter(Mandatory=$true)][string]$OrgUrl,
  [Parameter(Mandatory=$true)][string]$Token,
  [Parameter(Mandatory=$true)][string]$DllPath,
  [string]$SolutionUniqueName = "DataverseLastEmailParser",
  [string]$SolutionFriendly   = "Dataverse Last Email Parser",
  [string]$PublisherUnique    = "DataverseLastEmailParserPublisher",
  [string]$PublisherFriendly  = "Dataverse Last Email Parser (Community)",
  [string]$Prefix             = "dlep",
  [int]   $OptionValuePrefix  = 72700,
  [string]$AssemblyName       = "D365EmailParser.Plugins",
  [string]$PluginTypeName     = "D365EmailParser.Plugins.GetLatestEmailWithPlaceholders",
  [string]$CustomApiName      = "dlep_GetLatestEmailWithPlaceholders"
)

$ErrorActionPreference = "Stop"
$api = "$($OrgUrl.TrimEnd('/'))/api/data/v9.2"
$H = @{ Authorization = "Bearer $Token"; Accept = "application/json";
        "OData-MaxVersion"="4.0"; "OData-Version"="4.0" }

function Get-One($path) {
  $r = Invoke-RestMethod -Method GET -Uri "$api/$path" -Headers $H
  if ($r.value) { return $r.value[0] } else { return $null }
}
function New-Rec($set, $body, $solution) {
  $h = $H.Clone(); $h["Content-Type"]="application/json"
  if ($solution) { $h["MSCRM.SolutionUniqueName"] = $solution }
  $resp = Invoke-WebRequest -Method POST -Uri "$api/$set" -Headers $h -Body ($body | ConvertTo-Json -Depth 6)
  $u = $resp.Headers["OData-EntityId"]; if ($u -is [array]) { $u = $u[0] }
  if ($u -match "\(([0-9a-fA-F-]{36})\)") { return $Matches[1] }
  return $null
}

Write-Host "== Publisher ==" -ForegroundColor Cyan
$pub = Get-One "publishers?`$select=publisherid,uniquename&`$filter=uniquename eq '$PublisherUnique'"
if ($pub) { $pubId = $pub.publisherid; Write-Host "  exists $pubId" }
else {
  $pubId = New-Rec "publishers" @{ uniquename=$PublisherUnique; friendlyname=$PublisherFriendly;
    customizationprefix=$Prefix; customizationoptionvalueprefix=$OptionValuePrefix }
  Write-Host "  created $pubId"
}

Write-Host "== Solution ==" -ForegroundColor Cyan
$sol = Get-One "solutions?`$select=solutionid,uniquename&`$filter=uniquename eq '$SolutionUniqueName'"
if ($sol) { Write-Host "  exists $($sol.solutionid)" }
else {
  $solId = New-Rec "solutions" @{ uniquename=$SolutionUniqueName; friendlyname=$SolutionFriendly;
    version="1.0.0.0"; "publisherid@odata.bind"="/publishers($pubId)" }
  Write-Host "  created $solId"
}

Write-Host "== Plugin assembly ==" -ForegroundColor Cyan
$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path $DllPath)))
$asmName = [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path $DllPath))
$pkt = (($asmName.GetPublicKeyToken() | ForEach-Object { $_.ToString("x2") }) -join "")
if (-not $pkt) { throw "Assembly is not strong-named (no public key token). Rebuild merging with /keyfile:plugin.snk." }
$ver = $asmName.Version.ToString()
Write-Host "  public key token: $pkt  version: $ver"
$asm = Get-One "pluginassemblies?`$select=pluginassemblyid&`$filter=name eq '$AssemblyName'"
if ($asm) {
  $asmId = $asm.pluginassemblyid
  $h = $H.Clone(); $h["Content-Type"]="application/json"
  Invoke-WebRequest -Method PATCH -Uri "$api/pluginassemblies($asmId)" -Headers $h `
    -Body (@{ content=$b64; version=$ver } | ConvertTo-Json) | Out-Null
  Write-Host "  updated $asmId"
} else {
  $asmId = New-Rec "pluginassemblies" @{ name=$AssemblyName; content=$b64; isolationmode=2;
    sourcetype=0; culture="neutral"; version=$ver; publickeytoken=$pkt } $SolutionUniqueName
  Write-Host "  created $asmId"
}

Write-Host "== Plugin type ==" -ForegroundColor Cyan
$pt = Get-One "plugintypes?`$select=plugintypeid&`$filter=typename eq '$PluginTypeName'"
if ($pt) { $ptId = $pt.plugintypeid; Write-Host "  exists $ptId" }
else {
  $ptId = New-Rec "plugintypes" @{ typename=$PluginTypeName; friendlyname=$PluginTypeName;
    name=$PluginTypeName; "pluginassemblyid@odata.bind"="/pluginassemblies($asmId)" }
  Write-Host "  created $ptId"
}

Write-Host "== Custom API ==" -ForegroundColor Cyan
$capi = Get-One "customapis?`$select=customapiid&`$filter=uniquename eq '$CustomApiName'"
if ($capi) { $capiId = $capi.customapiid; Write-Host "  exists $capiId" }
else {
  $capiId = New-Rec "customapis" @{
    uniquename=$CustomApiName; name=$CustomApiName; displayname="Get Latest Email With Placeholders";
    description="Returns only the latest message of a concatenated Dataverse email thread, with embedded attachment GUIDs (latest message only) and inline placeholder tokens.";
    bindingtype=0; boundentitylogicalname=$null; isfunction=$false; isprivate=$false;
    allowedcustomprocessingsteptype=0; executeprivilegename=$null;
    "PluginTypeId@odata.bind"="/plugintypes($ptId)" } $SolutionUniqueName
  Write-Host "  created $capiId"
}

function Ensure-ReqParam($unique,$disp,$type,$optional) {
  $ex = Get-One "customapirequestparameters?`$select=customapirequestparameterid&`$filter=uniquename eq '$unique' and _customapiid_value eq $capiId"
  if ($ex) { Write-Host "  req $unique exists"; return }
  New-Rec "customapirequestparameters" @{ uniquename=$unique; name=$unique; displayname=$disp;
    type=$type; isoptional=$optional; logicalentityname=$null; "CustomAPIId@odata.bind"="/customapis($capiId)" } $SolutionUniqueName | Out-Null
  Write-Host "  req $unique created"
}
function Ensure-RespProp($unique,$disp,$type) {
  $ex = Get-One "customapiresponseproperties?`$select=customapiresponsepropertyid&`$filter=uniquename eq '$unique' and _customapiid_value eq $capiId"
  if ($ex) { Write-Host "  resp $unique exists"; return }
  New-Rec "customapiresponseproperties" @{ uniquename=$unique; name=$unique; displayname=$disp;
    type=$type; "CustomAPIId@odata.bind"="/customapis($capiId)" } $SolutionUniqueName | Out-Null
  Write-Host "  resp $unique created"
}

Write-Host "== Request parameters ==" -ForegroundColor Cyan
Ensure-ReqParam "EmailId"           "Email Id"           10 $false
Ensure-ReqParam "PlaceholderFormat" "Placeholder Format" 10 $true

Write-Host "== Response properties ==" -ForegroundColor Cyan
Ensure-RespProp "LatestText"                 "Latest Text"                   10
Ensure-RespProp "LatestTextWithPlaceholders" "Latest Text With Placeholders" 10
Ensure-RespProp "LatestHtml"                 "Latest Html"                   10
Ensure-RespProp "AttachmentIds"              "Attachment Ids (JSON)"         10

Write-Host "DONE. Custom API '$CustomApiName' deployed to solution '$SolutionUniqueName'." -ForegroundColor Green
