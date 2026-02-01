# Build script for WhisperKey MSI
# This script generates WiX components from the publish folder and builds the MSI

param(
    [string]$Version = "1.0.0",
    [string]$PublishDir = "./publish",
    [string]$OutputMsi = "./WhisperKey.msi"
)

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
    # Truncate to 72 characters max (WiX limit)
    if ($sanitized.Length -gt 72) {
        $sanitized = $sanitized.Substring(0, 72)
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
wix build ./Installer/WhisperKey.wxs ./Installer/Files.wxs -o $OutputMsi

if ($LASTEXITCODE -eq 0) {
    Write-Host "MSI built successfully: $OutputMsi" -ForegroundColor Green
} else {
    Write-Host "MSI build failed!" -ForegroundColor Red
    exit 1
}
