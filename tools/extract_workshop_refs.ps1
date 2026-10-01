<#
.SYNOPSIS
    Copies the compiled code of installed mods into src/refs-workshop so the
    Bereft mods build against exactly what you play with.

.DESCRIPTION
    Reads each mod's .tmod file from the Steam Workshop (or your tModLoader
    Mods folder) and extracts its main .dll plus any lib/*.dll it ships.
    Nothing is downloaded and the mods are never rebuilt.  When a mod has no
    extracted copy, the projects fall back to its source in src/refs.

    Run it again whenever one of these mods updates on the Workshop:
        powershell -ExecutionPolicy Bypass -File tools/extract_workshop_refs.ps1

.PARAMETER WorkshopPath
    Folder holding tModLoader's Workshop items (…\steamapps\workshop\content\1281930).
    Found from Steam's install and library folders when omitted.
#>
param(
    [string] $WorkshopPath
)

$ErrorActionPreference = 'Stop'

# Mods the Bereft projects compile against.
$mods = @('CalamityMod', 'SOTS', 'FargowiltasSouls', 'Luminance', 'StructureHelper', 'QuestBooks')

$root = Split-Path -Parent $PSScriptRoot
$out  = Join-Path $root 'src/refs-workshop'

function Find-WorkshopFolders {
    if ($WorkshopPath) { return @($WorkshopPath) }

    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if (-not $steam) { return @() }

    $libraries = @($steam)
    $vdf = Join-Path $steam 'steamapps/libraryfolders.vdf'
    if (Test-Path $vdf) {
        foreach ($match in Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"') {
            $libraries += $match.Matches[0].Groups[1].Value -replace '\\\\', '\'
        }
    }

    $libraries | Select-Object -Unique |
        ForEach-Object { Join-Path $_ 'steamapps/workshop/content/1281930' } |
        Where-Object { Test-Path $_ }
}

function Read-TmodString([System.IO.BinaryReader] $reader) { $reader.ReadString() }

# .tmod layout: "TMOD", tModLoader version, 20-byte hash, 256-byte signature,
# data length, then mod name, mod version, a file table (path, length,
# compressed length) and each file's bytes, deflated when the lengths differ.
function Expand-Tmod([string] $path, [string] $modName, [string] $destination) {
    $stream = [System.IO.File]::OpenRead($path)
    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        $magic = [System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(4))
        if ($magic -ne 'TMOD') { throw "$path is not a .tmod file" }

        $null = Read-TmodString $reader     # tModLoader version
        $null = $reader.ReadBytes(20 + 256) # hash and signature
        $null = $reader.ReadInt32()         # data length
        $name    = Read-TmodString $reader
        $version = Read-TmodString $reader

        $count = $reader.ReadInt32()
        $entries = for ($i = 0; $i -lt $count; $i++) {
            [pscustomobject]@{
                Path       = (Read-TmodString $reader)
                Length     = $reader.ReadInt32()
                Compressed = $reader.ReadInt32()
            }
        }

        New-Item -ItemType Directory -Force $destination | Out-Null
        foreach ($entry in $entries) {
            $bytes = $reader.ReadBytes($entry.Compressed)
            $wanted = $entry.Path -eq "$modName.dll" -or ($entry.Path -like 'lib/*.dll')
            if (-not $wanted) { continue }

            if ($entry.Compressed -ne $entry.Length) {
                $packed  = New-Object System.IO.MemoryStream(, $bytes)
                $deflate = New-Object System.IO.Compression.DeflateStream($packed, [System.IO.Compression.CompressionMode]::Decompress)
                $output = New-Object System.IO.MemoryStream
                $deflate.CopyTo($output)
                $bytes = $output.ToArray()
            }

            $file = Join-Path $destination (Split-Path -Leaf $entry.Path)
            [System.IO.File]::WriteAllBytes($file, $bytes)
        }

        return $version
    }
    finally {
        $stream.Dispose()
    }
}

# Every installed .tmod, newest first, so the latest Workshop version (or a
# newer manual copy in the Mods folder) is the one used.
$documents = [Environment]::GetFolderPath('MyDocuments')
$searchRoots = @(Join-Path $documents 'My Games/Terraria/tModLoader/Mods') + @(Find-WorkshopFolders)
$tmods = foreach ($folder in $searchRoots) {
    if (Test-Path $folder) { Get-ChildItem $folder -Recurse -Filter *.tmod }
}
$tmods = $tmods | Sort-Object LastWriteTime -Descending

if (-not $tmods) {
    throw 'No installed mods found.  Subscribe to the mods on the Workshop, or pass -WorkshopPath.'
}

if (Test-Path $out) { Remove-Item -Recurse -Force $out }

foreach ($mod in $mods) {
    $tmod = $tmods | Where-Object { $_.BaseName -eq $mod } | Select-Object -First 1
    if (-not $tmod) {
        Write-Host "  $mod`: not installed, the build will use src/refs/$mod instead" -ForegroundColor Yellow
        continue
    }

    $version = Expand-Tmod $tmod.FullName $mod (Join-Path $out $mod)
    Write-Host "  $mod $version  ($($tmod.FullName))"
}

Write-Host "Extracted to $out"
