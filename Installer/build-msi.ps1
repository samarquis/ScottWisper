# Fully Fixed Build script for WhisperKey MSI
param(
    [string]$Version = "1.0.0.0",
    [string]$PublishDir = "./publish",
    [string]$OutputMsi = "./WhisperKey.msi"
)

Write-Host "Building WhisperKey MSI v$Version..." -ForegroundColor Green

if (-not (Test-Path $PublishDir)) {
    Write-Host "Error: Publish directory $PublishDir not found!" -ForegroundColor Red
    exit 1
}

$script:componentRefs = @()
$script:fileCounter = 1
$script:dirCounter = 1

function Sanitize-WixId {
    param([string]$id)
    $sanitized = $id -replace '[^a-zA-Z0-9_]', '_'
    if ($sanitized -match '^[0-9]') { $sanitized = "_$sanitized" }
    if ($sanitized.Length -gt 50) { $sanitized = $sanitized.Substring(0, 50) }
    return $sanitized
}

function Generate-WixDir {
    param($Path, $Indent)
    $xml = ""
    
    # Process files in current directory
    Get-ChildItem -Path $Path -File | ForEach-Object {
        $safeName = Sanitize-WixId -id ($_.Name)
        $fileId = "F$($script:fileCounter)_$safeName"
        $xml += "$Indent  <Component Id=`"$fileId`" Guid=`"*`">`n"
        $xml += "$Indent    <File Id=`"$fileId`_f`" Source=`"$($_.FullName)`" KeyPath=`"yes`" />`n"
        $xml += "$Indent  </Component>`n"
        $script:componentRefs += "      <ComponentRef Id=`"$fileId`" />"
        $script:fileCounter++
    }
    
    # Process subdirectories
    Get-ChildItem -Path $Path -Directory | ForEach-Object {
        $safeDirName = Sanitize-WixId -id ($_.Name)
        $dirId = "D$($script:dirCounter)_$safeDirName"
        $xml += "$Indent  <Directory Id=`"$dirId`" Name=`"$($_.Name)`">`n"
        $xml += Generate-WixDir -Path $_.FullName -Indent "$Indent  "
        $xml += "$Indent  </Directory>`n"
        $script:dirCounter++
    }
    
    return $xml
}

# Resolve publish path to full path
$publishFullPath = Resolve-Path $PublishDir
$dirContentXml = Generate-WixDir -Path $publishFullPath -Indent "        "

$filesXml = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="WhisperKey">
$dirContentXml
      </Directory>
    </StandardDirectory>
    <ComponentGroup Id="ProductComponents">
$($script:componentRefs -join "`n")
    </ComponentGroup>
  </Fragment>
</Wix>
"@

$filesXml | Out-File -FilePath "./Installer/Files.wxs" -Encoding UTF8
Write-Host "Generated hierarchical Files.wxs with $($script:fileCounter - 1) files" -ForegroundColor Yellow

# Build the MSI
Write-Host "Building MSI..." -ForegroundColor Green
wix build ./Installer/WhisperKey.wxs ./Installer/Files.wxs -ext WixToolset.UI.wixext/4.0.5 -arch x64 -d Version="$Version" -o $OutputMsi

if ($LASTEXITCODE -eq 0) {
    Write-Host "MSI built successfully: $OutputMsi" -ForegroundColor Green
} else {
    Write-Host "MSI build failed!" -ForegroundColor Red
}
