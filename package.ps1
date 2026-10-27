# Builds the mod and creates release zips in dist\. Nothing is uploaded.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$source = Get-Content (Join-Path $root 'Plugin.cs') -Raw
if ($source -notmatch 'const string Version = "([^"]+)"') {
    throw "Couldn't find the version in Plugin.cs."
}
$version = $Matches[1]
$manifest = Get-Content (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.version_number -ne $version) {
    throw "Version mismatch: Plugin.cs has $version but manifest.json has $($manifest.version_number)."
}

dotnet build (Join-Path $root 'RelentlessSpecimens.csproj') -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}
$dll = Join-Path $root 'bin\RelentlessSpecimens.dll'

$dist = Join-Path $root 'dist'
$staging = Join-Path $dist 'staging'
if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}

# Manual install: unzip into the game folder.
$manual = Join-Path $staging 'manual'
New-Item -ItemType Directory -Force (Join-Path $manual 'BepInEx\plugins') | Out-Null
Copy-Item $dll (Join-Path $manual 'BepInEx\plugins')

# Thunderstore / r2modman package.
$thunderstore = Join-Path $staging 'thunderstore'
New-Item -ItemType Directory -Force $thunderstore | Out-Null
foreach ($file in 'manifest.json', 'icon.png', 'README.md', 'CHANGELOG.md') {
    Copy-Item (Join-Path $root $file) $thunderstore
}
Copy-Item $dll $thunderstore

# Entries are written by hand so paths use forward slashes, which mod managers expect.
# (Windows PowerShell's Compress-Archive and ZipFile.CreateFromDirectory can write backslashes.)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function New-Zip([string]$folder, [string]$zip) {
    if (Test-Path $zip) {
        Remove-Item $zip -Force
    }
    $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $base = (Resolve-Path $folder).Path.TrimEnd('\') + '\'
        foreach ($file in Get-ChildItem $folder -Recurse -File) {
            $entry = $file.FullName.Substring($base.Length).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "Created $zip"
}
New-Zip $manual (Join-Path $dist "RelentlessSpecimens-$version.zip")
New-Zip $thunderstore (Join-Path $dist "RelentlessSpecimens-$version-thunderstore.zip")
Remove-Item $staging -Recurse -Force
