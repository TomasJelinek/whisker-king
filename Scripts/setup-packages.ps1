# Whisker King Package Setup Script
# PowerShell script to automate Unity package installation and validation

param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.*\Editor\Unity.exe",
    [string]$ProjectPath = ".",
    [switch]$ValidateOnly = $false,
    [switch]$InstallRequired = $false,
    [switch]$InstallOptional = $false,
    [switch]$UpdateAll = $false,
    [switch]$GenerateReport = $false
)

# Configuration
$LogFile = "package-setup.log"
$PackageReportFile = "package-report.json"

# Package definitions matching the Unity PackageManager.cs script
$RequiredPackages = @{
    "com.unity.render-pipelines.universal" = "14.0.8"
    "com.unity.inputsystem" = "1.7.0"
    "com.unity.test-framework" = "1.1.33"
    "com.unity.addressables" = "1.21.14"
    "com.unity.analytics" = "3.8.1"
    "com.unity.textmeshpro" = "3.0.6"
    "com.unity.timeline" = "1.7.4"
    "com.unity.cinemachine" = "2.9.7"
    "com.unity.burst" = "1.8.4"
    "com.unity.mathematics" = "1.2.6"
    "com.unity.collections" = "1.4.0"
    "com.unity.jobs" = "0.51.0"
}

$OptionalPackages = @{
    "com.unity.ide.visualstudio" = "2.0.18"
    "com.unity.ide.vscode" = "1.2.5"
    "com.unity.ide.rider" = "3.0.24"
    "com.unity.collab-proxy" = "2.0.5"
    "com.unity.recorder" = "4.0.1"
    "com.unity.performance.profile-analyzer" = "1.2.2"
}

# Helper Functions
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    Add-Content -Path $LogFile -Value $logMessage
}

function Test-UnityInstallation {
    $unityExe = Get-ChildItem -Path (Split-Path $UnityPath) -Filter "Unity.exe" -Recurse | Select-Object -First 1
    if ($unityExe) {
        $script:UnityPath = $unityExe.FullName
        Write-Log "Unity found at: $($script:UnityPath)"
        return $true
    } else {
        Write-Log "Unity not found at specified path: $UnityPath" "ERROR"
        return $false
    }
}

function Test-ProjectStructure {
    $requiredPaths = @(
        "Assets",
        "Packages",
        "ProjectSettings"
    )
    
    foreach ($path in $requiredPaths) {
        if (-not (Test-Path (Join-Path $ProjectPath $path))) {
            Write-Log "Missing required project folder: $path" "ERROR"
            return $false
        }
    }
    
    Write-Log "Project structure validated"
    return $true
}

function Get-InstalledPackages {
    $manifestPath = Join-Path $ProjectPath "Packages\manifest.json"
    
    if (Test-Path $manifestPath) {
        try {
            $manifest = Get-Content $manifestPath | ConvertFrom-Json
            return $manifest.dependencies
        } catch {
            Write-Log "Error reading package manifest: $($_.Exception.Message)" "ERROR"
            return $null
        }
    } else {
        Write-Log "Package manifest not found: $manifestPath" "ERROR"
        return $null
    }
}

function Test-PackageRequirements {
    Write-Log "Validating package requirements..."
    
    $installedPackages = Get-InstalledPackages
    if (-not $installedPackages) {
        return $false
    }
    
    $missingRequired = @()
    $versionMismatches = @()
    
    # Check required packages
    foreach ($package in $RequiredPackages.GetEnumerator()) {
        $packageName = $package.Key
        $expectedVersion = $package.Value
        
        if ($installedPackages.PSObject.Properties.Name -contains $packageName) {
            $installedVersion = $installedPackages.$packageName
            if ($installedVersion -ne $expectedVersion) {
                $versionMismatches += @{
                    Package = $packageName
                    Expected = $expectedVersion
                    Installed = $installedVersion
                }
                Write-Log "Version mismatch: $packageName (Expected: $expectedVersion, Installed: $installedVersion)" "WARN"
            } else {
                Write-Log "Package OK: $packageName @ $installedVersion"
            }
        } else {
            $missingRequired += $packageName
            Write-Log "Missing required package: $packageName" "ERROR"
        }
    }
    
    # Summary
    Write-Log "=== Package Validation Summary ==="
    Write-Log "Total required packages: $($RequiredPackages.Count)"
    Write-Log "Missing required packages: $($missingRequired.Count)"
    Write-Log "Version mismatches: $($versionMismatches.Count)"
    
    if ($missingRequired.Count -gt 0) {
        Write-Log "Missing packages: $($missingRequired -join ', ')" "ERROR"
    }
    
    return ($missingRequired.Count -eq 0 -and $versionMismatches.Count -eq 0)
}

function Install-PackageViaUnity {
    param([string]$PackageName, [string]$Version)
    
    $packageId = "$PackageName@$Version"
    Write-Log "Installing package: $packageId"
    
    # Unity command-line package installation
    $arguments = @(
        "-batchmode",
        "-quit",
        "-projectPath", "`"$ProjectPath`"",
        "-logFile", "`"$LogFile`"",
        "-executeMethod", "WhiskerKing.Editor.PackageManager.InstallPackageCommand",
        "-packageId", $packageId
    )
    
    try {
        $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -eq 0) {
            Write-Log "Successfully installed: $packageId"
            return $true
        } else {
            Write-Log "Failed to install: $packageId (Exit code: $($process.ExitCode))" "ERROR"
            return $false
        }
    } catch {
        Write-Log "Error installing package $packageId : $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Install-RequiredPackages {
    Write-Log "Installing required packages for Whisker King..."
    
    $success = $true
    foreach ($package in $RequiredPackages.GetEnumerator()) {
        if (-not (Install-PackageViaUnity $package.Key $package.Value)) {
            $success = $false
        }
    }
    
    if ($success) {
        Write-Log "All required packages installed successfully"
    } else {
        Write-Log "Some required packages failed to install" "ERROR"
    }
    
    return $success
}

function Install-OptionalPackages {
    Write-Log "Installing optional packages for Whisker King..."
    
    $success = $true
    foreach ($package in $OptionalPackages.GetEnumerator()) {
        if (-not (Install-PackageViaUnity $package.Key $package.Value)) {
            # Optional packages failing is not critical
            Write-Log "Optional package failed to install: $($package.Key)" "WARN"
        }
    }
    
    Write-Log "Optional package installation completed"
    return $success
}

function Update-AllPackages {
    Write-Log "Updating all packages to latest versions..."
    
    # This would implement the actual update logic
    # For now, just log the action
    Write-Log "Package update functionality requires Unity Package Manager API" "WARN"
    Write-Log "Use Unity Editor menu: WhiskerKing → Packages → Update All Packages"
}

function Generate-PackageReport {
    Write-Log "Generating package report..."
    
    $installedPackages = Get-InstalledPackages
    if (-not $installedPackages) {
        return $false
    }
    
    $report = @{
        GeneratedDate = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        ProjectPath = $ProjectPath
        UnityPath = $UnityPath
        RequiredPackages = @()
        OptionalPackages = @()
        OtherPackages = @()
    }
    
    # Categorize packages
    foreach ($packageProperty in $installedPackages.PSObject.Properties) {
        $packageName = $packageProperty.Name
        $version = $packageProperty.Value
        
        $packageInfo = @{
            Name = $packageName
            Version = $version
            IsInstalled = $true
        }
        
        if ($RequiredPackages.ContainsKey($packageName)) {
            $packageInfo.ExpectedVersion = $RequiredPackages[$packageName]
            $packageInfo.IsCorrectVersion = ($version -eq $RequiredPackages[$packageName])
            $report.RequiredPackages += $packageInfo
        } elseif ($OptionalPackages.ContainsKey($packageName)) {
            $packageInfo.ExpectedVersion = $OptionalPackages[$packageName]
            $packageInfo.IsCorrectVersion = ($version -eq $OptionalPackages[$packageName])
            $report.OptionalPackages += $packageInfo
        } else {
            $report.OtherPackages += $packageInfo
        }
    }
    
    # Save report to file
    $reportJson = $report | ConvertTo-Json -Depth 3
    Set-Content -Path $PackageReportFile -Value $reportJson
    
    Write-Log "Package report generated: $PackageReportFile"
    Write-Log "Required packages: $($report.RequiredPackages.Count)"
    Write-Log "Optional packages: $($report.OptionalPackages.Count)"
    Write-Log "Other packages: $($report.OtherPackages.Count)"
    
    return $true
}

# Main Script Logic
function Main {
    Write-Log "=== Whisker King Package Setup Script ==="
    Write-Log "Project Path: $ProjectPath"
    Write-Log "Unity Path: $UnityPath"
    
    # Validate environment
    if (-not (Test-UnityInstallation)) {
        Write-Log "Unity installation validation failed" "ERROR"
        exit 1
    }
    
    if (-not (Test-ProjectStructure)) {
        Write-Log "Project structure validation failed" "ERROR"
        exit 1
    }
    
    # Execute requested operations
    if ($ValidateOnly) {
        Write-Log "Running package validation only..."
        $isValid = Test-PackageRequirements
        if ($isValid) {
            Write-Log "All package requirements satisfied"
            exit 0
        } else {
            Write-Log "Package validation failed" "ERROR"
            exit 1
        }
    }
    
    if ($InstallRequired) {
        Write-Log "Installing required packages..."
        if (-not (Install-RequiredPackages)) {
            Write-Log "Required package installation failed" "ERROR"
            exit 1
        }
    }
    
    if ($InstallOptional) {
        Write-Log "Installing optional packages..."
        Install-OptionalPackages
    }
    
    if ($UpdateAll) {
        Write-Log "Updating all packages..."
        Update-AllPackages
    }
    
    if ($GenerateReport) {
        Write-Log "Generating package report..."
        Generate-PackageReport
    }
    
    # Final validation
    Write-Log "Running final package validation..."
    $finalValidation = Test-PackageRequirements
    
    if ($finalValidation) {
        Write-Log "Package setup completed successfully!"
        exit 0
    } else {
        Write-Log "Package setup completed with warnings" "WARN"
        exit 2
    }
}

# Script Examples and Usage
if ($args.Count -eq 0 -and -not ($ValidateOnly -or $InstallRequired -or $InstallOptional -or $UpdateAll -or $GenerateReport)) {
    Write-Host @"
Whisker King Package Setup Script

Usage Examples:
  .\setup-packages.ps1 -ValidateOnly                 # Check package requirements
  .\setup-packages.ps1 -InstallRequired              # Install required packages
  .\setup-packages.ps1 -InstallOptional              # Install optional packages
  .\setup-packages.ps1 -InstallRequired -InstallOptional  # Install all packages
  .\setup-packages.ps1 -GenerateReport               # Generate package report
  .\setup-packages.ps1 -UpdateAll                    # Update all packages

Parameters:
  -UnityPath        Path to Unity executable
  -ProjectPath      Path to Unity project (default: current directory)
  -ValidateOnly     Only validate packages, don't install
  -InstallRequired  Install required packages
  -InstallOptional  Install optional packages  
  -UpdateAll        Update all packages to latest versions
  -GenerateReport   Generate detailed package report

Examples:
  # Quick validation
  .\setup-packages.ps1 -ValidateOnly
  
  # Fresh project setup
  .\setup-packages.ps1 -InstallRequired -InstallOptional -GenerateReport
  
  # CI/CD validation
  .\setup-packages.ps1 -UnityPath "C:\Unity\2022.3.0f1\Editor\Unity.exe" -ValidateOnly
"@
    exit 0
}

# Run main function
Main
