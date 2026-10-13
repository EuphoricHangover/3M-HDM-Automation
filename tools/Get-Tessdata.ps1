<#
.SYNOPSIS
    Downloads the English OCR model (eng.traineddata) for Tesseract into the tessdata folder.

.DESCRIPTION
    Uses the "fast" model from the official tesseract-ocr/tessdata_fast repository (Apache-2.0).
    Prints the file's SHA-256 so you can record it. On a locked-down computer, download the file
    on another machine and copy it into the tessdata folder instead.

.PARAMETER Destination
    Folder to put eng.traineddata in. Defaults to ..\tessdata relative to this script.
#>
[CmdletBinding()]
param([string]$Destination = (Join-Path $PSScriptRoot '..\tessdata'))

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$target = Join-Path $Destination 'eng.traineddata'
$url = 'https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata'

Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing
$size = (Get-Item $target).Length
if ($size -lt 1MB) {
    Remove-Item $target -Force
    throw "Download looks wrong ($size bytes). Expected a few MB; the file was removed."
}
Write-Host "Saved $target ($([math]::Round($size / 1MB, 1)) MB)"
Write-Host "SHA-256: $((Get-FileHash $target -Algorithm SHA256).Hash)"
