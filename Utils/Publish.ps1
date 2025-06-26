##
# Publish.ps1
#
# The script publishes all bots in the AdvancedDemos folder to the 'Distribution' folder for supported runtimes.
#
# Examples:
#   .\Publish.ps1                        # to publish all bots in the AdvancedDemos folder.
#   .\Publish.ps1 -runtime linux-x64     # to publish all bots in the AdvancedDemos folder for only the linux-x64 runtime.
##

[CmdletBinding()]
param
(
    # Parameter to select runtimes for which to publish the bots.
    [ValidateSet("win-x86", "win-x64", "linux-x64", "osx-x64", "osx-arm64")][string]$runtime = $null
)

Set-StrictMode -Version 3
# Stop the script when a first error is encountered.
$ErrorActionPreference = "Stop"

Set-StrictMode -Version 3

# Stop the script when a first error is encountered.
$ErrorActionPreference = "Stop"

# Define the root folder (parent of Utils folder).
$rootFolder = Split-Path -Path $PSScriptRoot -Parent

# Define the paths.
$advancedDemosFolder = Join-Path -Path $rootFolder -ChildPath "AdvancedDemos"
$distributionFolder = Join-Path -Path $rootFolder -ChildPath "Distribution"

# Define projects and their desired executable names.
# Format: @{"<relative path to .csproj>" = "<desired executable name>"}
$projectMap = @{
    "DCA/WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.DCA.csproj" = "WsBotDca";
    "MomentumBreakout/WhalesSecret.ScriptApiLib.Samples.AdvancedDemos.MomentumBreakout.csproj" = "WsBotMomentumBreakout"
}

# Runtimes to target.
if ($runtime -eq $null) {
    $runtimes = @("win-x86", "win-x64", "linux-x64", "osx-x64", "osx-arm64")
} else {
    $runtimes = @($runtime)
}

# Create Distribution folder if it doesn't exist.
if (-not (Test-Path -Path $distributionFolder)) {
    New-Item -ItemType Directory -Path $distributionFolder | Out-Null
    Write-Host "# Created Distribution folder at '$distributionFolder'."
}

# Function to get the default executable name from the .csproj file.
function Get-DefaultExecutableName {
    param (
        [string]$csprojPath
    )
    $csprojContent = Get-Content -Path $csprojPath -Raw
    if ($csprojContent -match '<AssemblyName>(.*?)</AssemblyName>') {
        return $Matches[1]
    }
    elseif ($csprojContent -match '<TargetName>(.*?)</TargetName>') {
        return $Matches[1]
    }
    else {
        # Fallback to project file name without .csproj extension.
        return [System.IO.Path]::GetFileNameWithoutExtension($csprojPath)
    }
}

# Delete all contents of the Distribution folder if it exists.
if (Test-Path -Path $distributionFolder) {
    try {
        Remove-Item -Path "$distributionFolder\*" -Recurse -Force
        Write-Host "# Cleared contents of Distribution folder at '$distributionFolder'."
    }
    catch {
        Write-Error "Failed to clear contents of Distribution folder '$_'."
        exit 1
    }
}

# Publish each project for each runtime.
foreach ($project in $projectMap.GetEnumerator()) {
    $projectKey = $project.Key
    $projectPath = Join-Path -Path $advancedDemosFolder -ChildPath $projectKey
    $customExeName = $project.Value

    if (-not (Test-Path -Path $projectPath)) {
        Write-Error "Project not found. Path '$projectPath' does not exist."
        continue
    }

    # Define the project-specific input.json path.
    $projectFolder = Split-Path -Path $projectPath -Parent
    $inputJsonPath = Join-Path -Path $projectFolder -ChildPath "input.json"

    # Get the default executable name from the .csproj file.
    $defaultExeName = Get-DefaultExecutableName -csprojPath $projectPath
    Write-Host "# Publishing project '$projectKey' (Default: '$defaultExeName', Custom: '$customExeName')" -ForegroundColor Green

    # Check if input.json exists for the project.
    if (-not (Test-Path -Path $inputJsonPath)) {
        Write-Warning "input.json not found for project at '$inputJsonPath'. Skipping copy for this project."
        $skipJsonCopy = $true
    }
    else {
        $skipJsonCopy = $false
        Write-Host "# Found input.json for project at '$inputJsonPath'."
    }

    foreach ($runtime in $runtimes) {
        Write-Host "# Publishing '$projectKey' for runtime '$runtime'."

        # Define output folder for this project and runtime.
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectKey)
        $outputFolder = Join-Path -Path $distributionFolder -ChildPath "$customExeName/$runtime"
        
        # Create output folder if it doesn't exist.
        if (-not (Test-Path -Path $outputFolder)) {
            New-Item -ItemType Directory -Path $outputFolder | Out-Null
        }

        # Run dotnet publish.
        $publishCommand = "dotnet publish `"$projectPath`" -c Release -r $runtime --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o `"$outputFolder`""
        
        Write-Host "## [$customExeName][$runtime] Executing command '$publishCommand'."
        Invoke-Expression $publishCommand

        if ($LASTEXITCODE -eq 0) {
            Write-Host "## [$customExeName][$runtime] Successfully published '$projectName' to '$outputFolder'."
            
            # Determine the extension based on the runtime.
            $exeExtension = if ($runtime -like "win*") { ".exe" } else { "" }
            
            # Paths for default and custom executables.
            $defaultExePath = Join-Path -Path $outputFolder -ChildPath "$defaultExeName$exeExtension"
            $customExePath = Join-Path -Path $outputFolder -ChildPath "$customExeName$exeExtension"

            # Rename the executable if it exists.
            if (Test-Path -Path $defaultExePath) {
                try {
                    Rename-Item -Path $defaultExePath -NewName "$customExeName$exeExtension" -Force
                    Write-Host "## [$customExeName][$runtime] Renamed executable to '$customExePath'."
                }
                catch {
                    Write-Error "[$customExeName][$runtime] Failed to rename '$defaultExePath' to '$customExePath' : $_"
                    continue
                }
            }
            else {
                Write-Warning "[$customExeName][$runtime] Default executable '$defaultExePath' not found!"
                continue
            }

            # Delete .pdb and .xml files.
            $filesToDelete = Get-ChildItem -Path $outputFolder -Include *.pdb,*.xml -Recurse
            foreach ($file in $filesToDelete) {
                try {
                    Remove-Item -Path $file.FullName -Force
                    Write-Host "## [$customExeName][$runtime] Deleted file '$($file.FullName)'."
                }
                catch {
                    Write-Error "[$customExeName][$runtime] Failed to delete '$($file.FullName)': $_"
                }
            }

            # Copy project-specific input.json to the publish folder.
            if (-not $skipJsonCopy) {
                $destinationJsonPath = Join-Path -Path $outputFolder -ChildPath "input.json"
                try {
                    Copy-Item -Path $inputJsonPath -Destination $destinationJsonPath -Force
                    Write-Host "## [$customExeName][$runtime] Copied input.json to '$destinationJsonPath'."
                }
                catch {
                    Write-Error "[$customExeName][$runtime] Failed to copy input.json to '${destinationJsonPath}': $_"
                }
            }

            # Create a zip file named <customExeName>.<runtime>.zip in the Distribution folder.
            $zipFileName = "${customExeName}.${runtime}.zip"
            $zipFilePath = Join-Path -Path $distributionFolder -ChildPath $zipFileName
            try {
                Compress-Archive -Path "$outputFolder\*" -DestinationPath $zipFilePath -Force
                Write-Host "## [$customExeName][$runtime] Created zip file '$zipFilePath'."

                # Calculate SHA256 hash of the zip file.
                try {
                    $hash = Get-FileHash -Path $zipFilePath -Algorithm SHA256
                    $hashLine = "$($hash.Hash)  $zipFileName"
                    $hashFilePath = Join-Path -Path $distributionFolder -ChildPath "hashes.txt"
                    Add-Content -Path $hashFilePath -Value $hashLine
                    Write-Host "## [$customExeName][$runtime] Recorded SHA256 hash for '$zipFileName' in '$hashFilePath'."
                }
                catch {
                    Write-Error "[$customExeName][$runtime] Failed to calculate or record SHA256 hash for '${zipFilePath}': $_"
                }
            }
            catch {
                Write-Error "[$customExeName][$runtime] Failed to create zip file '${zipFilePath}': $_"
            }
        }
        else {
            Write-Error "[$customExeName][$runtime] Failed to publish '$projectName' for '$runtime'."
        }
    }
}

Write-Host "# Publishing, renaming, cleanup, and file copying completed! See folder '$distributionFolder'." -ForegroundColor Cyan