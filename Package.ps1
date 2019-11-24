Write-Host -BackgroundColor White -ForegroundColor Black "Marsher Packaging Tool"

$ReleasePath = Join-Path $PSScriptRoot 'Marsher\bin\Release'
$TempPath = Join-Path $PSScriptRoot 'Package-Temp'
$PublishPath = Join-Path $PSScriptRoot 'Publish'
$ReleasingPath = Join-Path $PublishPath 'Releasing'
$Exclude = '(.+\.(pdb|xml|application|manifest))|(app\.publish)'

if (Test-Path $TempPath) {
    Write-Host "Temp path already exists, removing..."
    Remove-Item -Path $TempPath -Recurse
}
New-Item -Path $PSScriptRoot -Name 'Package-Temp' -ItemType 'directory' | Out-Null
Write-Host "Collecting built binaries to $($TempPath)"
Get-ChildItem $ReleasePath | 
    Where-Object{$_.Name -notmatch $Exclude} | 
    Copy-Item -Destination $TempPath -Recurse -Force

$MainExecutable = Join-Path $TempPath 'Marsher.exe'
if (-not {Test-Path $MainExecutable}) {
    Write-Host -BackgroundColor Red -ForegroundColor White "Marsher main executable not found! Please build the solution before executing this script!"
}
$MainExecutableVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($MainExecutable).FileVersion
$MainExecutableVersion = $MainExecutableVersion.Substring(0, $MainExecutableVersion.LastIndexOf('.'))
Write-Host "Found Marsher main executable $($MainExecutable), version $($MainExecutableVersion)"

$NuspecPath = Join-Path $PublishPath 'Marsher.nuspec'
$NuGetPackagePath = Join-Path $PublishPath "Marsher.$($MainExecutableVersion).nupkg"
Write-Host "Packing NuGet package using nuspec $($NuspecPath)..."
nuget pack $NuspecPath -Version $MainExecutableVersion -Properties Configuration=Release -OutputDirectory $PublishPath -BasePath $TempPath

$IconPath = Join-Path $PSScriptRoot 'Marsher\Resources\Icons\Logo.ico'
$AnimationPath = Join-Path $PublishPath 'InstallAnimation.gif'
Write-Host "Releasifying Squirrel package..."
$SetupPath = Join-Path $ReleasingPath 'Setup.exe'
$NewSetupPath = Join-Path $ReleasingPath "Marsher-Setup-$($MainExecutableVersion).exe"
if (Test-Path $NewSetupPath) {
    Remove-Item -Path $NewSetupPath
}
squirrel -r $ReleasingPath --releasify $NuGetPackagePath -i $IconPath --framework-version=net472 -n '/a /tr http://tsa.wotrus.com/rfc3161' -g $AnimationPath --no-msi
Rename-Item -Path $SetupPath "Marsher-Setup-$($MainExecutableVersion).exe"
Write-Host -BackgroundColor Green -ForegroundColor White "Successfully packaged Marsher $($MainExecutableVersion)!"