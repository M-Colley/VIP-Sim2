<#
.SYNOPSIS
    Sign VIP-Sim.exe and its bundled native plug-ins for Windows distribution.

.DESCRIPTION
    Without a signature, SmartScreen shows "Windows protected your PC" on every download
    and the user has to click through "More info > Run anyway". For a tool distributed to
    study participants or sold, that is not acceptable -- most people stop at that screen.

    Signs the plug-in DLLs as well as the executable. Unity ships uWindowCapture and the
    MediaPipe natives as loose DLLs inside VIP-Sim_Data/Plugins; leaving those unsigned
    means the signed executable loads unsigned code, which some enterprise policies block
    outright even though the .exe itself verifies.

    Uses RFC 3161 timestamping. Without a timestamp the signature expires with the
    certificate and every previously shipped build stops validating; with one it stays
    valid after the certificate lapses.

.PARAMETER BuildDir
    Folder containing VIP-Sim.exe.

.PARAMETER CertPath
    Path to a .pfx code-signing certificate.

.PARAMETER CertPassword
    Password for the .pfx. Prompted for securely if omitted -- do not pass this on a
    command line you would rather not have in your shell history.

.EXAMPLE
    .\sign-windows.ps1 -BuildDir "C:\...\VIP-Sim-Windows-Build" -CertPath "C:\certs\vipsim.pfx"

.NOTES
    Requires signtool.exe from the Windows SDK.
    A certificate must be bought from a CA -- typically 200-400 USD/year. An EV
    certificate additionally carries instant SmartScreen reputation; a standard OV one
    accumulates reputation over time and downloads may still be warned about at first.
#>
param(
    [Parameter(Mandatory = $true)][string]$BuildDir,
    [Parameter(Mandatory = $true)][string]$CertPath,
    [SecureString]$CertPassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

$exe = Join-Path $BuildDir "VIP-Sim.exe"
if (-not (Test-Path $exe))   { throw "VIP-Sim.exe not found in $BuildDir" }
if (-not (Test-Path $CertPath)) { throw "certificate not found: $CertPath" }

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "x64" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1 -ExpandProperty FullName
if (-not $signtool) { throw "signtool.exe not found. Install the Windows SDK (Signing Tools component)." }

if (-not $CertPassword) { $CertPassword = Read-Host "Certificate password" -AsSecureString }
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
             [Runtime.InteropServices.Marshal]::SecureStringToBSTR($CertPassword))

# Plug-ins first. The executable is signed last so that verifying it also verifies a
# bundle whose native dependencies are already signed.
$targets = @()
$plugins = Join-Path $BuildDir "VIP-Sim_Data\Plugins"
if (Test-Path $plugins) {
    $targets += Get-ChildItem $plugins -Recurse -Include *.dll | Select-Object -ExpandProperty FullName
}
$targets += $exe

Write-Host "Signing $($targets.Count) file(s) with $(Split-Path $CertPath -Leaf)"
foreach ($t in $targets) {
    & $signtool sign /f $CertPath /p $plain /fd SHA256 /tr $TimestampUrl /td SHA256 /q $t
    if ($LASTEXITCODE -ne 0) { throw "signing failed: $t" }
}

Write-Host "Verifying"
& $signtool verify /pa /v $exe
if ($LASTEXITCODE -ne 0) { throw "verification failed" }

Write-Host ""
Write-Host "Done. $exe is signed and timestamped."
Write-Host "SmartScreen reputation builds over time on a standard certificate; an EV"
Write-Host "certificate carries it immediately."
