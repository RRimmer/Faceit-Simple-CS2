# Builds Faceit-Simple-cs2 and packs Faceit-Simple-CS2-<ver>.zip in the repository root.
$ErrorActionPreference = "Stop"
$Name = "Faceit-Simple-cs2"
$Root = $PSScriptRoot
$Proj = Join-Path $Root $Name
$Csproj = Join-Path $Proj "$Name.csproj"
# Version comes from <Version> in the csproj, so the zip name always matches the dll.
$Ver = ([xml](Get-Content $Csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $Ver) { throw "Version not found in $Csproj" }
$Out = Join-Path $Root "release-staging\$Name-$Ver"
$Zip = Join-Path $Root "Faceit-Simple-CS2-$Ver.zip"
$Log = Join-Path $Root "pack.log"

Start-Transcript -Path $Log -Force | Out-Null
try {
    Write-Host "=== build $Ver ==="
    dotnet build $Csproj -c Release -nologo
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    $bin = Join-Path $Proj "bin\Release\net10.0"

    Write-Host "=== assemble ==="
    if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
    $plug = Join-Path $Out "Server-plugins\counterstrikesharp\plugins\$Name"
    $cfg = Join-Path $Out "Server-plugins\counterstrikesharp\configs\plugins\$Name"
    $src = Join-Path $Out "Source\$Name"
    New-Item -ItemType Directory -Force -Path $plug, $cfg, $src | Out-Null

    Copy-Item (Join-Path $bin "$Name.dll"), (Join-Path $bin "$Name.deps.json"), (Join-Path $bin "$Name.pdb") $plug
    Copy-Item (Join-Path $bin "lang") $plug -Recurse
    Copy-Item (Join-Path $Root "$Name.example.jsonc") (Join-Path $cfg "$Name.jsonc")
    Copy-Item (Join-Path $Root "Content-addonmanager") $Out -Recurse
    Copy-Item (Join-Path $Root "INSTALL.md"), (Join-Path $Root "CHANGELOG.md"), (Join-Path $Root "README.md") $Out

    Copy-Item (Join-Path $Proj "*.cs"), $Csproj $src
    Copy-Item (Join-Path $Proj "lang") $src -Recurse
    Copy-Item (Join-Path $Root "pack.cmd"), (Join-Path $Root "pack.ps1"), (Join-Path $Root "$Name.example.jsonc") (Join-Path $Out "Source")

    # Backups and logs never go into the archive.
    Get-ChildItem $Out -Recurse -File -Include "*.bak", "*.log" | Remove-Item -Force
    # CounterStrikeSharp.API.dll must never be shipped next to the plugin.
    Get-ChildItem (Join-Path $Out "Server-plugins") -Recurse -Include "CounterStrikeSharp.API.dll" | Remove-Item -Force

    Write-Host "=== zip ==="
    # ZipArchive with "/" in entry names: Compress-Archive on Windows PowerShell 5 writes "\".
    if (Test-Path $Zip) { Remove-Item $Zip -Force }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $fs = [System.IO.File]::Open($Zip, [System.IO.FileMode]::Create)
    $archive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $Out -Recurse -File | ForEach-Object {
            $entry = $_.FullName.Substring($Out.Length + 1).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $archive.Dispose()
        $fs.Dispose()
    }
    Write-Host "OK $Zip"
}
finally {
    Stop-Transcript | Out-Null
}
