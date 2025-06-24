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
$runtimes = @("win-x86", "win-x64", "linux-x64", "osx-x64")

# Create Distribution folder if it doesn't exist.
if (-not (Test-Path -Path $distributionFolder)) {
    New-Item -ItemType Directory -Path $distributionFolder | Out-Null
    Write-Host "Created Distribution folder at: $distributionFolder"
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

# Publish each project for each runtime.
foreach ($project in $projectMap.GetEnumerator()) {
    $projectPath = Join-Path -Path $advancedDemosFolder -ChildPath $project.Key
    $customExeName = $project.Value

    if (-not (Test-Path -Path $projectPath)) {
        Write-Host "Project not found: $projectPath" -ForegroundColor Red
        continue
    }

    # Define the project-specific input.json path.
    $projectFolder = Split-Path -Path $projectPath -Parent
    $inputJsonPath = Join-Path -Path $projectFolder -ChildPath "input.json"

    # Check if input.json exists for the project.
    if (-not (Test-Path -Path $inputJsonPath)) {
        Write-Host "Warning: input.json not found for project at $inputJsonPath. Skipping copy for this project." -ForegroundColor Yellow
        $skipJsonCopy = $true
    }
    else {
        $skipJsonCopy = $false
        Write-Host "Found input.json for project at: $inputJsonPath"
    }

    # Get the default executable name from the .csproj file.
    $defaultExeName = Get-DefaultExecutableName -csprojPath $projectPath
    Write-Host "Publishing project: $project.Key (Default: $defaultExeName, Custom: $customExeName)"

    foreach ($runtime in $runtimes) {
        Write-Host "  Publishing for runtime: $runtime"

        # Define output folder for this project and runtime.
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.Key)
        $outputFolder = Join-Path -Path $distributionFolder -ChildPath "$customExeName/$runtime"
        
        # Create output folder if it doesn't exist.
        if (-not (Test-Path -Path $outputFolder)) {
            New-Item -ItemType Directory -Path $outputFolder | Out-Null
        }

        # Run dotnet publish.
        $publishCommand = "dotnet publish `"$projectPath`" -c Release -r $runtime --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o `"$outputFolder`""
        
        Write-Host "  Executing: $publishCommand"
        Invoke-Expression $publishCommand

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Successfully published $projectName for $runtime to $outputFolder" -ForegroundColor Green
            
            # Determine the extension based on the runtime.
            $exeExtension = if ($runtime -like "win*") { ".exe" } else { "" }
            
            # Paths for default and custom executables.
            $defaultExePath = Join-Path -Path $outputFolder -ChildPath "$defaultExeName$exeExtension"
            $customExePath = Join-Path -Path $outputFolder -ChildPath "$customExeName$exeExtension"

            # Rename the executable if it exists.
            if (Test-Path -Path $defaultExePath) {
                try {
                    Rename-Item -Path $defaultExePath -NewName "$customExeName$exeExtension" -Force
                    Write-Host "  Renamed executable to: $customExePath" -ForegroundColor Green
                }
                catch {
                    Write-Host "  Failed to rename $defaultExePath to $customExePath : $_" -ForegroundColor Red
                    continue
                }
            }
            else {
                Write-Host "  Warning: Default executable $defaultExePath not found!" -ForegroundColor Yellow
                continue
            }

            # Delete .pdb and .xml files.
            $filesToDelete = Get-ChildItem -Path $outputFolder -Include *.pdb,*.xml -Recurse
            foreach ($file in $filesToDelete) {
                try {
                    Remove-Item -Path $file.FullName -Force
                    Write-Host "  Deleted: $($file.FullName)" -ForegroundColor Green
                }
                catch {
                    Write-Host "  Failed to delete $($file.FullName): $_" -ForegroundColor Red
                }
            }

            # Copy project-specific input.json to the publish folder.
            if (-not $skipJsonCopy) {
                $destinationJsonPath = Join-Path -Path $outputFolder -ChildPath "input.json"
                try {
                    Copy-Item -Path $inputJsonPath -Destination $destinationJsonPath -Force
                    Write-Host "  Copied input.json to: $destinationJsonPath" -ForegroundColor Green
                }
                catch {
                    Write-Host "  Failed to copy input.json to ${destinationJsonPath}: $_" -ForegroundColor Red
                }
            }
        }
        else {
            Write-Host "  Failed to publish $projectName for $runtime" -ForegroundColor Red
        }
    }
}

Write-Host "Publishing, renaming, cleanup, and file copying completed!" -ForegroundColor Cyan