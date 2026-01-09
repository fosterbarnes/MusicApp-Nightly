$Host.UI.RawUI.WindowTitle = "MusicApp Nightly Release"
. $PROFILE; Center-PSWindow

# Find the newest build folder automatically
$basePath = "C:\Users\Foster\Documents\Visual Studio Projects\MusicApp-Nightly\Nightly Builds"

# Get all version folders and find the newest one based on version number
$buildFolders = Get-ChildItem -Path $basePath -Directory | Where-Object { $_.Name -match "^[0-9]+\.[0-9]+\.[0-9]+$" }

if ($buildFolders.Count -eq 0) {
    Write-Host "Error: No build folders found in $basePath" -ForegroundColor Red
    exit 1
}

# Sort folders by version number (major.minor.patch)
$newestFolder = $buildFolders | ForEach-Object {
    $version = $_.Name
    $versionParts = $version -split "\."
    [PSCustomObject]@{
        Folder = $_
        Version = $version
        Major = [int]$versionParts[0]
        Minor = [int]$versionParts[1]
        Patch = [int]$versionParts[2]
    }
} | Sort-Object Major, Minor, Patch -Descending | Select-Object -First 1

$newestFolderPath = $newestFolder.Folder.FullName
$newestFolderName = $newestFolder.Folder.Name

# Prompt the user for the build folder
Write-Host "Newest build folder found: $newestFolderName"
$userInput = Read-Host "Press enter to use newest build, or enter desired folder:"

# Determine the full build folder path based on user input
if ([string]::IsNullOrWhiteSpace($userInput)) {
    # User pressed enter, use the newest folder
    $buildFolder = $newestFolderPath
    Write-Host "Using newest build folder: $newestFolderName"
} elseif ($userInput -match "^[0-9]+\.[0-9]+\.[0-9]+$") {
    # User entered just version number (e.g., "8.6.25")
    $version = $userInput
    $buildFolder = "$basePath\$version"
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

Write-Host "Using build folder: $buildFolder"

# Extract version number from folder name (now just the folder name itself)
$version = Split-Path $buildFolder -Leaf
if (-not ($version -match "^[0-9]+\.[0-9]+\.[0-9]+$")) {
    Write-Host "Error: Could not extract version number from folder name: $version" -ForegroundColor Red
    exit 1
}

Write-Host "Extracted version: $version"

# Check for releaseNotes.txt file in the build folder
$releaseNotesPath = "$buildFolder\releaseNotes.txt"
$releaseNotes = $null
$hasReleaseNotesFile = $false

if (Test-Path $releaseNotesPath) {
    Write-Host "`nFound releaseNotes.txt file. Reading contents..." -ForegroundColor Green
    try {
        # Read the file content and preserve formatting
        $releaseNotes = Get-Content -Path $releaseNotesPath -Raw -Encoding UTF8
        if ($releaseNotes) {
            # Convert tab characters to spaces for proper formatting (same as manual entry)
            $releaseNotes = $releaseNotes -replace "`t", "    "
            $hasReleaseNotesFile = $true
            Write-Host "Successfully loaded release notes from file." -ForegroundColor Green
        }
    } catch {
        Write-Host "Warning: Could not read releaseNotes.txt file. Error: $($_.Exception.Message)" -ForegroundColor Yellow
        $hasReleaseNotesFile = $false
    }
}

# If no releaseNotes.txt file was found or couldn't be read, prompt user for release notes
if (-not $hasReleaseNotesFile) {
    Write-Host "`nEnter release notes (press Enter twice to finish, or just press Enter to use default notes):" -ForegroundColor Yellow
    Write-Host "You can paste multi-line formatted text. Press Enter twice when done." -ForegroundColor Cyan
    Write-Host "Note: Tab characters will be converted to spaces for proper formatting." -ForegroundColor Cyan

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
            # Convert tab characters to spaces for proper formatting
            $line = $line -replace "`t", "    "
            $releaseNotesLines += $line
            $consecutiveEmptyLines = 0
            $hasEnteredContent = $true
        }
    }

    # If no custom release notes were entered, use the default
    if (-not $hasEnteredContent) {
        $releaseNotes = "Nightly build release for version $version."
        Write-Host "Using default release notes: $releaseNotes"
    } else {
        $releaseNotes = $releaseNotesLines -join "`n"
        Write-Host "Using custom release notes (${releaseNotesLines.Count} lines)"
    }
} else {
    Write-Host "Using release notes from file: releaseNotes.txt" -ForegroundColor Green
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
