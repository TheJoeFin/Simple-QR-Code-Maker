#requires -Version 7.4
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$strings = Join-Path (Split-Path $PSScriptRoot -Parent) 'Simple QR Code Maker\Strings'
$source = Join-Path $strings 'en-us\Resources.resw'
$latin = 'áƀçđéƒĝħíĵķĺɱñóþɋŕšţúṽŵẋýž'
$rtl = 'אבגדהוזחטיךכלםמןנסעףפץצקרש'
$protected = '\{\d+(?:,-?\d+)?(?::[^{}]*)?\}|https?://[^\s<>"\])]+|[\w.+-]+@[\w.-]+\.[\p{L}]{2,}'

function Convert-ToPseudoText([string] $Text, [bool] $Mirrored) {
    $alphabet = if ($Mirrored) { $rtl } else { $latin }
    $protectedParts = [regex]::Matches($Text, $protected)
    $result = [System.Text.StringBuilder]::new()
    $offset = 0
    $letters = 0
    for ($partIndex = 0; $partIndex -le $protectedParts.Count; $partIndex++) {
        $part = if ($partIndex -lt $protectedParts.Count) { $protectedParts[$partIndex] } else { $null }
        $end = if ($null -ne $part) { $part.Index } else { $Text.Length }
        for (; $offset -lt $end; $offset++) {
            $character = $Text[$offset]
            $index = [int][char]::ToLowerInvariant($character) - [int][char]'a'
            if ($index -ge 0 -and $index -lt 26) {
                $replacement = $alphabet[$index]
                if (!$Mirrored -and [char]::IsUpper($character)) {
                    $replacement = [char]::ToUpperInvariant($replacement)
                }
                [void]$result.Append($replacement)
                $letters++
            } else {
                [void]$result.Append($character)
            }
        }
        if ($null -ne $part) {
            if ($Mirrored) { [void]$result.Append([char]0x2066) }
            [void]$result.Append($part.Value)
            if ($Mirrored) { [void]$result.Append([char]0x2069) }
            $offset += $part.Length
        }
    }
    if ($letters -eq 0) { return $Text }

    $leading = [regex]::Match($Text, '^\s*').Value
    $trailing = [regex]::Match($Text, '\s*$').Value
    $padding = $alphabet[23].ToString() * [int][Math]::Ceiling($letters * 0.4)
    $expanded = "$leading[$($result.ToString().Trim()) $padding]$trailing"
    if ($Mirrored) { return ([string][char]0x2067) + $expanded + [char]0x2069 }
    return $expanded
}

foreach ($locale in @('qps-ploc', 'qps-plocm')) {
    $mirrored = $locale -eq 'qps-plocm'
    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    $document.Load($source)
    foreach ($data in $document.SelectNodes('/root/data')) {
        $key = $data.GetAttribute('name')
        $value = $data.SelectSingleNode('value')
        if ($key -eq 'App_FlowDirection') {
            $value.InnerText = if ($mirrored) { 'RightToLeft' } else { 'LeftToRight' }
        } elseif ($key -notlike '*.AccessKey' -and $key -notin @('Package_TileShortName', 'GenerateIds_FormatGuid', 'GenerateIds_FormatNanoId')) {
            $value.InnerText = Convert-ToPseudoText $value.InnerText $mirrored
        }
    }

    $directory = Join-Path $strings $locale
    [void][IO.Directory]::CreateDirectory($directory)
    $path = Join-Path $directory 'Resources.resw'
    $document.Save($path)
    Write-Output "Generated $locale ($($document.SelectNodes('/root/data').Count) keys)."
}
