[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ReportPath,
    [Parameter(Mandatory)]
    [ValidateSet('Core', 'Windows')]
    [string] $Component
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$thresholds = Get-Content -Raw (Join-Path $root 'tests\thresholds.json') | ConvertFrom-Json
$minimum = $thresholds.$Component
[xml] $report = Get-Content -Raw $ReportPath
$packages = @($report.coverage.packages.package | Where-Object { $_.name -eq "Simple QR Code Maker.$Component" })
if ($packages.Count -ne 1) {
    throw "Expected exactly one production coverage package for $Component in $ReportPath."
}

$failed = $false
foreach ($metric in @('line', 'branch')) {
    $percentage = 100 * [double]::Parse($packages[0].GetAttribute("$metric-rate"), [cultureinfo]::InvariantCulture)
    if (-not [double]::IsFinite($percentage) -or $percentage -lt 0 -or $percentage -gt 100) {
        throw "Invalid $metric coverage in $ReportPath."
    }
    '{0} {1} coverage: {2:F2}% (minimum {3}%)' -f $Component, $metric, $percentage, $minimum.$metric
    if ($percentage -lt $minimum.$metric) {
        $failed = $true
    }
}
if ($failed) {
    throw "$Component coverage is below the committed thresholds in tests\thresholds.json."
}
