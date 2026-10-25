[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(),
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [ValidateSet('All', 'Core', 'Windows')]
    [string] $Suite = 'All',
    [switch] $Coverage
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$suites = if ($Suite -eq 'All') {
    if ($IsWindows) { @('Core', 'Windows') } else { @('Core') }
} else {
    @($Suite)
}
if ('Windows' -in $suites -and -not $IsWindows) {
    throw 'The Windows suite requires Windows. Use -Suite Core on this platform.'
}

$failed = $false
foreach ($name in $suites) {
    $project = Join-Path $root "tests\Simple QR Code Maker.$name.Tests\Simple QR Code Maker.$name.Tests.csproj"
    $results = Join-Path $root "TestResults\$name"
    if (Test-Path $results) {
        Get-ChildItem $results -File |
            Where-Object { $_.Name -in @('results.trx', 'coverage.cobertura.xml') } |
            Remove-Item
    }
    $arguments = @(
        'test', '--project', $project,
        '--configuration', $Configuration, "-p:Platform=$Platform",
        '--minimum-expected-tests', '1',
        '--results-directory', $results,
        '--report-trx', '--report-trx-filename', 'results.trx'
    )
    if ($Coverage) {
        $arguments += @(
            '--coverage', '--coverage-output-format', 'cobertura',
            '--coverage-output', (Join-Path $results 'coverage.cobertura.xml'),
            '--coverage-settings', (Join-Path $root 'tests\coverage.config')
        )
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        $failed = $true
    } elseif ($Coverage) {
        try {
            & (Join-Path $PSScriptRoot 'Assert-Coverage.ps1') -Component $name -ReportPath (Join-Path $results 'coverage.cobertura.xml')
        } catch {
            Write-Error -Message $_.Exception.Message -ErrorAction Continue
            $failed = $true
        }
    }
}

if ($failed) {
    throw 'One or more test suites or coverage gates failed. See TestResults for details.'
}
