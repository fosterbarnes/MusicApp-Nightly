$Host.UI.RawUI.WindowTitle = "MusicApp Nightly Release"
. $PROFILE; Center-PSWindow

# Prompt the user for the build folder
$userInput = Read-Host "Enter build folder (ex: '8.6.25')"

# Determine the full build folder path based on user input
$basePath = "C:\Users\Foster\Documents\Visual Studio Projects\MusicApp-Nightly\Nightly Builds"

if ($userInput -match "^[0-9]+\.[0-9]+\.[0-9]+$") {
    # User entered just version number (e.g., "8.6.25")
    $version = $userInput
    $buildFolder = "$basePath\MusicApp $version"
} elseif ($userInput -match "^MusicApp [0-9]+\.[0-9]+\.[0-9]+$") {
    # User entered folder name (e.g., "MusicApp 8.6.25")
    $buildFolder = "$basePath\$userInput"
} elseif (Test-Path $userInput) {
    # User entered a full path that exists
    $buildFolder = $userInput
} else {
    # Try to construct the path assuming it's a folder name
    $buildFolder = "$basePath\$userInput"
}

# Check if the build folder exists
if (-not (Test-Path $buildFolder)) {
    Write-Host "Error: Build folder not found: $buildFolder" -ForegroundColor Red
    Write-Host "Please check the path and try again." -ForegroundColor Red
    exit 1
}

Write-Host "Using build folder: $buildFolder" -ForegroundColor Green

# Extract version number from folder name (assuming it's always the last part like "MusicApp 8.4.25")
$version = ($buildFolder -split "MusicApp ")[-1]
if (-not $version) {
    # If the split didn't work, try to extract version from the folder name
    $folderName = Split-Path $buildFolder -Leaf
    $version = ($folderName -split "MusicApp ")[-1]
    if (-not $version) {
        Write-Host "Error: Could not extract version number from folder name: $folderName" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Extracted version: $version" -ForegroundColor Green

# Prompt for release notes
Write-Host "`nEnter release notes (press Enter twice to finish, or just press Enter to use default notes):" -ForegroundColor Yellow
Write-Host "You can paste multi-line formatted text. Press Enter twice when done." -ForegroundColor Cyan

$releaseNotesLines = @()
$consecutiveEmptyLines = 0
$hasEnteredContent = $false

while ($true) {
    $line = Read-Host ">"
    
    if ($line -eq "") {
        $consecutiveEmptyLines++
        if ($consecutiveEmptyLines -ge 2) {
            break
        }
        # Add empty line to preserve formatting
        $releaseNotesLines += ""
    } else {
        $releaseNotesLines += $line
        $consecutiveEmptyLines = 0
        $hasEnteredContent = $true
    }
}

# If no custom release notes were entered, use the default
if (-not $hasEnteredContent) {
    $releaseNotes = "Nightly build release for version $version."
    Write-Host "Using default release notes: $releaseNotes" -ForegroundColor Green
} else {
    $releaseNotes = $releaseNotesLines -join "`n"
    Write-Host "Using custom release notes (${releaseNotesLines.Count} lines)" -ForegroundColor Green
}

# Construct paths and release details
$bin = "$buildFolder\bin\Debug\net8.0-windows"
$zipPath = "$env:TEMP\MusicApp_${version}_Build.zip"
$binZipPath = "$env:TEMP\MusicApp_${version}_Release.zip"
$tagName = "v$version"
$releaseName = "$version Nightly Release"

# Compress the build folder and binary folder using 7-Zip (moderate compression)
& 7z a -tzip -mx=5 "$zipPath" "$buildFolder\*"
& 7z a -tzip -mx=5 "$binZipPath" "$bin\*"

# Change to local repo root
Set-Location "C:\Users\Foster\Documents\Visual Studio Projects\MusicApp-Nightly"

# Check and delete existing tags if they exist (local and remote)
if (git tag -l $tagName) {
    Write-Host "Local tag $tagName exists. Deleting..."
    git tag -d $tagName
}

$remoteTags = git ls-remote --tags origin | ForEach-Object { ($_ -split "`t")[1] }
if ($remoteTags -contains "refs/tags/$tagName") {
    Write-Host "Remote tag $tagName exists. Deleting..."
    git push origin --delete $tagName
}

# Create and push the new tag
git tag $tagName
git push origin $tagName

# Create the GitHub release and upload both zip files
& gh release create $tagName "$zipPath" "$binZipPath" --title "$releaseName" --notes "$releaseNotes"

# Clean up temporary zip files
Remove-Item -Path "$zipPath", "$binZipPath" -ErrorAction SilentlyContinue
Write-Host "Temporary zip files cleaned up."
