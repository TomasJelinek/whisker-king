#!/bin/bash
# Whisker King Package Setup Script
# Bash script for macOS/Linux Unity package management

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="${PROJECT_PATH:-$(dirname "$SCRIPT_DIR")}"
LOG_FILE="package-setup.log"
PACKAGE_REPORT_FILE="package-report.json"

# Default Unity paths by platform
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity}"
else
    # Linux
    UNITY_PATH="${UNITY_PATH:-/opt/unity/Editor/Unity}"
fi

# Package definitions (matching PowerShell script)
declare -A REQUIRED_PACKAGES=(
    ["com.unity.render-pipelines.universal"]="14.0.8"
    ["com.unity.inputsystem"]="1.7.0"
    ["com.unity.test-framework"]="1.1.33"
    ["com.unity.addressables"]="1.21.14"
    ["com.unity.analytics"]="3.8.1"
    ["com.unity.textmeshpro"]="3.0.6"
    ["com.unity.timeline"]="1.7.4"
    ["com.unity.cinemachine"]="2.9.7"
    ["com.unity.burst"]="1.8.4"
    ["com.unity.mathematics"]="1.2.6"
    ["com.unity.collections"]="1.4.0"
    ["com.unity.jobs"]="0.51.0"
)

declare -A OPTIONAL_PACKAGES=(
    ["com.unity.ide.visualstudio"]="2.0.18"
    ["com.unity.ide.vscode"]="1.2.5"
    ["com.unity.ide.rider"]="3.0.24"
    ["com.unity.collab-proxy"]="2.0.5"
    ["com.unity.recorder"]="4.0.1"
    ["com.unity.performance.profile-analyzer"]="1.2.2"
)

# Command line options
VALIDATE_ONLY=false
INSTALL_REQUIRED=false
INSTALL_OPTIONAL=false
UPDATE_ALL=false
GENERATE_REPORT=false

# Helper functions
log_message() {
    local level="$1"
    local message="$2"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    local log_line="[$timestamp] [$level] $message"
    
    echo "$log_line"
    echo "$log_line" >> "$LOG_FILE"
}

log_info() {
    log_message "INFO" "$1"
}

log_warn() {
    log_message "WARN" "$1"
}

log_error() {
    log_message "ERROR" "$1"
}

find_unity_installation() {
    log_info "Searching for Unity installation..."
    
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS - look for Unity Hub installations
        local unity_hub_path="/Applications/Unity/Hub/Editor"
        if [[ -d "$unity_hub_path" ]]; then
            local unity_exe=$(find "$unity_hub_path" -name "Unity" -type f 2>/dev/null | head -1)
            if [[ -n "$unity_exe" ]]; then
                UNITY_PATH="$unity_exe"
                log_info "Found Unity at: $UNITY_PATH"
                return 0
            fi
        fi
        
        # Try standard application path
        if [[ -f "/Applications/Unity/Unity.app/Contents/MacOS/Unity" ]]; then
            UNITY_PATH="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
            log_info "Found Unity at: $UNITY_PATH"
            return 0
        fi
    else
        # Linux
        local common_paths=(
            "/opt/unity/Editor/Unity"
            "/usr/bin/unity-editor"
            "$HOME/Unity/Hub/Editor/*/Editor/Unity"
        )
        
        for path in "${common_paths[@]}"; do
            if [[ -f "$path" ]]; then
                UNITY_PATH="$path"
                log_info "Found Unity at: $UNITY_PATH"
                return 0
            fi
        done
    fi
    
    log_error "Unity installation not found. Please set UNITY_PATH environment variable."
    return 1
}

validate_project_structure() {
    log_info "Validating project structure..."
    
    local required_dirs=("Assets" "Packages" "ProjectSettings")
    
    for dir in "${required_dirs[@]}"; do
        if [[ ! -d "$PROJECT_PATH/$dir" ]]; then
            log_error "Missing required project directory: $dir"
            return 1
        fi
    done
    
    log_info "Project structure validated"
    return 0
}

get_installed_packages() {
    local manifest_path="$PROJECT_PATH/Packages/manifest.json"
    
    if [[ ! -f "$manifest_path" ]]; then
        log_error "Package manifest not found: $manifest_path"
        return 1
    fi
    
    # Extract dependencies section from manifest.json
    if command -v jq > /dev/null 2>&1; then
        jq -r '.dependencies // {}' "$manifest_path" 2>/dev/null || {
            log_error "Error parsing package manifest"
            return 1
        }
    else
        log_warn "jq not found. Package validation will be limited."
        return 1
    fi
}

validate_package_requirements() {
    log_info "Validating package requirements..."
    
    local installed_packages
    installed_packages=$(get_installed_packages) || return 1
    
    local missing_count=0
    local mismatch_count=0
    
    # Check required packages
    for package_name in "${!REQUIRED_PACKAGES[@]}"; do
        local expected_version="${REQUIRED_PACKAGES[$package_name]}"
        
        if command -v jq > /dev/null 2>&1; then
            local installed_version
            installed_version=$(echo "$installed_packages" | jq -r --arg pkg "$package_name" '.[$pkg] // empty')
            
            if [[ -n "$installed_version" ]]; then
                if [[ "$installed_version" == "$expected_version" ]]; then
                    log_info "Package OK: $package_name @ $installed_version"
                else
                    log_warn "Version mismatch: $package_name (Expected: $expected_version, Installed: $installed_version)"
                    ((mismatch_count++))
                fi
            else
                log_error "Missing required package: $package_name"
                ((missing_count++))
            fi
        else
            # Fallback without jq
            if grep -q "\"$package_name\"" "$PROJECT_PATH/Packages/manifest.json"; then
                log_info "Package found: $package_name (version check requires jq)"
            else
                log_error "Missing required package: $package_name"
                ((missing_count++))
            fi
        fi
    done
    
    # Summary
    log_info "=== Package Validation Summary ==="
    log_info "Total required packages: ${#REQUIRED_PACKAGES[@]}"
    log_info "Missing required packages: $missing_count"
    log_info "Version mismatches: $mismatch_count"
    
    return $(( missing_count + mismatch_count ))
}

install_package_via_unity() {
    local package_name="$1"
    local version="$2"
    local package_id="${package_name}@${version}"
    
    log_info "Installing package: $package_id"
    
    # Unity command-line package installation
    local unity_args=(
        -batchmode
        -quit
        -projectPath "$PROJECT_PATH"
        -logFile "$LOG_FILE"
        -executeMethod "WhiskerKing.Editor.PackageManager.InstallPackageCommand"
        -packageId "$package_id"
    )
    
    if "$UNITY_PATH" "${unity_args[@]}"; then
        log_info "Successfully installed: $package_id"
        return 0
    else
        log_error "Failed to install: $package_id"
        return 1
    fi
}

install_required_packages() {
    log_info "Installing required packages for Whisker King..."
    
    local failed_count=0
    
    for package_name in "${!REQUIRED_PACKAGES[@]}"; do
        local version="${REQUIRED_PACKAGES[$package_name]}"
        if ! install_package_via_unity "$package_name" "$version"; then
            ((failed_count++))
        fi
    done
    
    if [[ $failed_count -eq 0 ]]; then
        log_info "All required packages installed successfully"
        return 0
    else
        log_error "$failed_count required packages failed to install"
        return 1
    fi
}

install_optional_packages() {
    log_info "Installing optional packages for Whisker King..."
    
    for package_name in "${!OPTIONAL_PACKAGES[@]}"; do
        local version="${OPTIONAL_PACKAGES[$package_name]}"
        if ! install_package_via_unity "$package_name" "$version"; then
            log_warn "Optional package failed to install: $package_name"
        fi
    done
    
    log_info "Optional package installation completed"
    return 0
}

update_all_packages() {
    log_info "Updating all packages to latest versions..."
    log_warn "Package update functionality requires Unity Package Manager API"
    log_info "Use Unity Editor menu: WhiskerKing → Packages → Update All Packages"
}

generate_package_report() {
    log_info "Generating package report..."
    
    local installed_packages
    installed_packages=$(get_installed_packages) || return 1
    
    if ! command -v jq > /dev/null 2>&1; then
        log_error "jq is required for package report generation"
        return 1
    fi
    
    local report=$(jq -n \
        --arg generated_date "$(date '+%Y-%m-%d %H:%M:%S')" \
        --arg project_path "$PROJECT_PATH" \
        --arg unity_path "$UNITY_PATH" \
        --argjson installed_packages "$installed_packages" \
        '{
            generatedDate: $generated_date,
            projectPath: $project_path,
            unityPath: $unity_path,
            requiredPackages: [],
            optionalPackages: [],
            otherPackages: []
        }')
    
    # This would need more complex jq processing to categorize packages
    # For now, just save the basic structure
    echo "$report" > "$PACKAGE_REPORT_FILE"
    
    log_info "Package report generated: $PACKAGE_REPORT_FILE"
    return 0
}

show_usage() {
    cat << EOF
Whisker King Package Setup Script

Usage: $0 [OPTIONS]

OPTIONS:
  -h, --help              Show this help message
  -v, --validate-only     Only validate packages, don't install
  -r, --install-required  Install required packages
  -o, --install-optional  Install optional packages
  -u, --update-all        Update all packages to latest versions
  -g, --generate-report   Generate detailed package report

ENVIRONMENT VARIABLES:
  PROJECT_PATH    Path to Unity project (default: parent of script directory)
  UNITY_PATH      Path to Unity executable (auto-detected if not set)

EXAMPLES:
  # Quick validation
  $0 --validate-only
  
  # Fresh project setup
  $0 --install-required --install-optional --generate-report
  
  # CI/CD validation with custom Unity path
  UNITY_PATH=/custom/unity/path $0 --validate-only

REQUIREMENTS:
  - Unity 2022.3 LTS or newer
  - jq (for JSON processing) - install via: brew install jq (macOS) or apt install jq (Ubuntu)
EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            show_usage
            exit 0
            ;;
        -v|--validate-only)
            VALIDATE_ONLY=true
            shift
            ;;
        -r|--install-required)
            INSTALL_REQUIRED=true
            shift
            ;;
        -o|--install-optional)
            INSTALL_OPTIONAL=true
            shift
            ;;
        -u|--update-all)
            UPDATE_ALL=true
            shift
            ;;
        -g|--generate-report)
            GENERATE_REPORT=true
            shift
            ;;
        *)
            log_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Main script logic
main() {
    log_info "=== Whisker King Package Setup Script ==="
    log_info "Project Path: $PROJECT_PATH"
    log_info "Unity Path: $UNITY_PATH"
    
    # Find Unity installation
    if [[ ! -f "$UNITY_PATH" ]]; then
        if ! find_unity_installation; then
            exit 1
        fi
    fi
    
    # Validate project structure
    if ! validate_project_structure; then
        exit 1
    fi
    
    # Execute requested operations
    if [[ "$VALIDATE_ONLY" == "true" ]]; then
        log_info "Running package validation only..."
        if validate_package_requirements; then
            log_info "All package requirements satisfied"
            exit 0
        else
            log_error "Package validation failed"
            exit 1
        fi
    fi
    
    if [[ "$INSTALL_REQUIRED" == "true" ]]; then
        log_info "Installing required packages..."
        if ! install_required_packages; then
            log_error "Required package installation failed"
            exit 1
        fi
    fi
    
    if [[ "$INSTALL_OPTIONAL" == "true" ]]; then
        log_info "Installing optional packages..."
        install_optional_packages
    fi
    
    if [[ "$UPDATE_ALL" == "true" ]]; then
        log_info "Updating all packages..."
        update_all_packages
    fi
    
    if [[ "$GENERATE_REPORT" == "true" ]]; then
        log_info "Generating package report..."
        generate_package_report
    fi
    
    # Final validation if any installation was performed
    if [[ "$INSTALL_REQUIRED" == "true" ]] || [[ "$INSTALL_OPTIONAL" == "true" ]]; then
        log_info "Running final package validation..."
        if validate_package_requirements; then
            log_info "Package setup completed successfully!"
            exit 0
        else
            log_warn "Package setup completed with warnings"
            exit 2
        fi
    fi
    
    # If no specific actions were requested, show usage
    if [[ "$VALIDATE_ONLY" == "false" ]] && [[ "$INSTALL_REQUIRED" == "false" ]] && \
       [[ "$INSTALL_OPTIONAL" == "false" ]] && [[ "$UPDATE_ALL" == "false" ]] && \
       [[ "$GENERATE_REPORT" == "false" ]]; then
        show_usage
        exit 0
    fi
}

# Run main function
main "$@"
