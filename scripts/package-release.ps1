[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$PackageLabel,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$RuntimeIdentifier = "win-x64",

    [string]$ProjectPath = "HDD-Index/HDD-Index.csproj"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$resolvedProjectPath = Join-Path $repositoryRoot $ProjectPath
$archiveName = "HDD-Index-$PackageLabel-$RuntimeIdentifier"
$publishDirectory = Join-Path $outputRoot "$archiveName-publish"
$packageDirectory = Join-Path $outputRoot $archiveName
$archivePath = Join-Path $outputRoot "$archiveName.zip"
$checksumPath = "$archivePath.sha256"

if (-not (Test-Path -LiteralPath $resolvedProjectPath -PathType Leaf)) {
    throw "Project file does not exist: $resolvedProjectPath"
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

foreach ($generatedPath in @(
    $publishDirectory,
    $packageDirectory,
    $archivePath,
    $checksumPath
)) {
    if (Test-Path -LiteralPath $generatedPath) {
        throw "Package output already exists: $generatedPath"
    }
}

Push-Location $repositoryRoot
try {
    dotnet publish $resolvedProjectPath `
        --configuration Release `
        --runtime $RuntimeIdentifier `
        --self-contained true `
        --no-restore `
        -p:Version=$Version `
        --output $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed."
    }

    New-Item -ItemType Directory -Path $packageDirectory | Out-Null
    Copy-Item -Path (Join-Path $publishDirectory "*") `
        -Destination $packageDirectory `
        -Recurse
    Copy-Item README.md, LICENSE -Destination $packageDirectory
    Copy-Item docs, screenshots `
        -Destination $packageDirectory `
        -Recurse

    foreach ($requiredPath in @(
        "HDD-Index.exe",
        "README.md",
        "LICENSE",
        "docs/releasing.md"
    )) {
        $fullRequiredPath = Join-Path $packageDirectory $requiredPath
        if (-not (Test-Path -LiteralPath $fullRequiredPath)) {
            throw "Required package content is missing: $requiredPath"
        }
    }

    Compress-Archive `
        -Path (Join-Path $packageDirectory "*") `
        -DestinationPath $archivePath `
        -CompressionLevel Optimal

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
        foreach ($requiredEntry in @(
            "HDD-Index.exe",
            "README.md",
            "LICENSE",
            "docs/releasing.md"
        )) {
            if ($entryNames -notcontains $requiredEntry) {
                throw "Required archive entry is missing: $requiredEntry"
            }
        }

        if (-not ($entryNames | Where-Object { $_.StartsWith("screenshots/") })) {
            throw "Required archive content is missing: screenshots/"
        }
    }
    finally {
        $archive.Dispose()
    }

    $archiveHash = (
        Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
    ).Hash.ToLowerInvariant()
    "$archiveHash  $([System.IO.Path]::GetFileName($archivePath))" |
        Set-Content -LiteralPath $checksumPath -Encoding ascii
}
finally {
    Pop-Location
}

Write-Output "Created release archive: $archivePath"
Write-Output "Created checksum: $checksumPath"
