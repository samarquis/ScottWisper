# Build script for WhisperKey MSI
# This script generates WiX components from the publish folder and builds the MSI
# Version is read from WhisperKey.csproj (single source of truth)

param(
    [string]$Version = "",  # If empty, will be read from .csproj
    [string]$PublishDir = "./publish",
    [string]$OutputMsi = "./WhisperKey.msi"
)

# Read version from .csproj if not provided
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csprojPath = Join-Path $PSScriptRoot "..\WhisperKey.csproj"
    if (Test-Path $csprojPath) {
        $csprojContent = Get-Content $csprojPath -Raw
        # Extract Version from <Version>1.0.0</Version>
        if ($csprojContent -match '<Version>([^<]+)</Version>') {
            $Version = $Matches[1]
            Write-Host "Version read from WhisperKey.csproj: $Version" -ForegroundColor Cyan
        } else {
            Write-Host "Warning: Could not find Version in .csproj, using default: 1.0.0" -ForegroundColor Yellow
            $Version = "1.0.0"
        }
    } else {
        Write-Host "Warning: WhisperKey.csproj not found at $csprojPath, using default version: 1.0.0" -ForegroundColor Yellow
        $Version = "1.0.0"
    }
}

Write-Host "Building WhisperKey MSI v$Version..." -ForegroundColor Green

# Generate file components
$files = Get-ChildItem -Path $PublishDir -File -Recurse
$components = @()
$componentRefs = @()
$dirCache = @{}

$filesXml = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <StandardDirectory Id="ProgramFiles6432Folder">
      <Directory Id="INSTALLFOLDER" Name="WhisperKey">
"@

function Sanitize-WixId {
    param([string]$id)
    # Replace invalid characters with underscores
    $sanitized = $id -replace '[^a-zA-Z0-9_]', '_'
    # Ensure it starts with a letter or underscore
    if ($sanitized -match '^[0-9]') {
        $sanitized = "_$sanitized"
    }
    # Truncate to 50 characters max to be safe for both Component and File IDs
    if ($sanitized.Length -gt 50) {
        $sanitized = $sanitized.Substring(0, 50)
    }
    return $sanitized
}

$fileCounter = 1
foreach ($file in $files) {
    $relativePath = $file.FullName.Substring((Resolve-Path $PublishDir).Path.Length + 1)
    # Create a safe ID using counter + sanitized filename
    $safeName = Sanitize-WixId -id ($file.Name)
    $fileId = "F$fileCounter`_" + $safeName
    $fileId = Sanitize-WixId -id $fileId
    
    # Truncate further if still too long
    if ($fileId.Length -gt 72) {
        $fileId = $fileId.Substring(0, 72)
    }
    
    $components += @"
        <Component Id="$fileId" Guid="*">
          <File Id="$fileId`_f" Source="$($file.FullName)" KeyPath="yes" />
        </Component>
"@
    $componentRefs += "      <ComponentRef Id=`"$fileId`" />`n"
    $fileCounter++
}

$filesXml += $components -join ""
$filesXml += @"
      </Directory>
    </StandardDirectory>
    <ComponentGroup Id="ProductComponents">
$componentRefs
    </ComponentGroup>
  </Fragment>
</Wix>
"@

$filesXml | Out-File -FilePath "./Installer/Files.wxs" -Encoding UTF8
Write-Host "Generated Files.wxs with $($files.Count) files" -ForegroundColor Yellow

# Build the MSI
Write-Host "Building MSI..." -ForegroundColor Green
wix build ./Installer/WhisperKey.wxs ./Installer/Files.wxs -d Version="$Version" -o $OutputMsi

if ($LASTEXITCODE -eq 0) {
    Write-Host "MSI built successfully: $OutputMsi" -ForegroundColor Green
} else {
    Write-Host "MSI build failed!" -ForegroundColor Red
    exit 1
}
