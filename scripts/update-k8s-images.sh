#!/bin/bash

# Kubernetes Image Update Script
# This script updates the image tags in the Kubernetes configuration

set -euo pipefail  # Exit on any error, undefined variables, or pipe failures

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color

# Configuration
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(dirname "${SCRIPT_DIR}")"
readonly K8S_CONFIG="${PROJECT_ROOT}/searchengine-minikube.yaml"
readonly BACKUP_SUFFIX=".backup"

# Function to print colored messages
print_info() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}$1${NC}"; }
print_warning() { echo -e "${YELLOW}$1${NC}"; }
print_error() { echo -e "${RED}$1${NC}"; }

# Function to display usage
show_usage() {
    echo "Usage: $0 [OPTIONS] <version>"
    echo ""
    echo "Arguments:"
    echo "  version              Docker image version to use (e.g., 1.0.0, latest)"
    echo ""
    echo "Options:"
    echo "  -f, --file FILE      Kubernetes YAML file to update (default: searchengine-minikube.yaml)"
    echo "  -n, --no-backup      Don't create a backup file"
    echo "  -a, --apply          Apply changes to Kubernetes after updating"
    echo "  -v, --verify         Show current image versions before and after update"
    echo "  -h, --help           Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 1.2.3                    # Update to version 1.2.3"
    echo "  $0 latest -a                # Update to latest and apply to k8s"
    echo "  $0 1.0.0 --no-backup -v     # Update without backup, show verification"
}

# Function to check prerequisites
check_prerequisites() {
    local k8s_file="$1"
    
    # Check if Kubernetes config file exists
    if [[ ! -f "$k8s_file" ]]; then
        print_error "Kubernetes configuration file not found: $k8s_file"
        print_info "Please ensure the file exists or specify a different file with -f option"
        exit 1
    fi
    
    # Check if file is writable
    if [[ ! -w "$k8s_file" ]]; then
        print_error "Cannot write to Kubernetes configuration file: $k8s_file"
        print_info "Please check file permissions"
        exit 1
    fi
}

# Function to validate version format (basic validation)
validate_version() {
    local version="$1"
    
    # Allow 'latest' or semantic versioning pattern
    if [[ "$version" == "latest" ]] || [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || [[ "$version" =~ ^[0-9]+\.[0-9]+$ ]] || [[ "$version" =~ ^[0-9]+$ ]]; then
        return 0
    else
        print_warning "Version format '$version' doesn't follow semantic versioning (e.g., 1.2.3)"
        print_warning "Continuing anyway..."
        return 0
    fi
}

# Function to show current image versions
show_current_versions() {
    local k8s_file="$1"
    
    print_info "Current image versions in $k8s_file:"
    if grep -E "image: (searchapi|searchweb):" "$k8s_file" >/dev/null 2>&1; then
        grep -E "image: (searchapi|searchweb):" "$k8s_file" | sed 's/^/  /'
    else
        print_warning "No SearchAPI or SearchWeb images found in the configuration"
    fi
}

# Function to create backup
create_backup() {
    local k8s_file="$1"
    local backup_file="${k8s_file}${BACKUP_SUFFIX}"
    
    if cp "$k8s_file" "$backup_file"; then
        print_success "✓ Backup created: $backup_file"
        return 0
    else
        print_error "Failed to create backup file"
        return 1
    fi
}

# Function to update image versions
update_images() {
    local k8s_file="$1"
    local version="$2"
    
    print_info "Updating image versions to: $version"
    
    # Count how many substitutions will be made
    local searchapi_count=$(grep -c "image: searchapi:" "$k8s_file" || true)
    local searchweb_count=$(grep -c "image: searchweb:" "$k8s_file" || true)
    
    if [[ $searchapi_count -eq 0 ]] && [[ $searchweb_count -eq 0 ]]; then
        print_warning "No SearchAPI or SearchWeb images found to update"
        return 1
    fi
    
    # Use different sed syntax for macOS vs Linux
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        sed -i '' "s|image: searchapi:.*|image: searchapi:${version}|g" "$k8s_file"
        sed -i '' "s|image: searchweb:.*|image: searchweb:${version}|g" "$k8s_file"
    else
        # Linux
        sed -i "s|image: searchapi:.*|image: searchapi:${version}|g" "$k8s_file"
        sed -i "s|image: searchweb:.*|image: searchweb:${version}|g" "$k8s_file"
    fi
    
    print_success "✓ Updated $searchapi_count SearchAPI image(s)"
    print_success "✓ Updated $searchweb_count SearchWeb image(s)"
    return 0
}

# Function to apply to Kubernetes
apply_to_kubernetes() {
    local k8s_file="$1"
    
    print_info "Applying changes to Kubernetes..."
    
    # Check if kubectl is available
    if ! command -v kubectl >/dev/null 2>&1; then
        print_error "kubectl is not installed or not in PATH"
        print_info "Please install kubectl to apply changes automatically"
        return 1
    fi
    
    # Check if kubectl can connect to cluster
    if ! kubectl cluster-info >/dev/null 2>&1; then
        print_error "Cannot connect to Kubernetes cluster"
        print_info "Please check your kubectl configuration"
        return 1
    fi
    
    if kubectl apply -f "$k8s_file"; then
        print_success "✓ Changes applied to Kubernetes successfully"
        return 0
    else
        print_error "Failed to apply changes to Kubernetes"
        return 1
    fi
}

# Function to show next steps
show_next_steps() {
    local k8s_file="$1"
    local version="$2"
    
    echo
    print_info "Next steps:"
    echo -e "  1. Review changes: ${BLUE}git diff $k8s_file${NC}"
    echo -e "  2. Apply to cluster: ${BLUE}kubectl apply -f $k8s_file${NC}"
    echo -e "  3. Check deployment: ${BLUE}kubectl get pods -n searchengine${NC}"
    echo -e "  4. View logs: ${BLUE}kubectl logs -n searchengine deployment/searchapi${NC}"
    echo
    print_warning "Remember to ensure the Docker images for version '$version' exist!"
    print_info "Build them with: ./scripts/build-docker-images.sh $version"
}

# Main function
main() {
    local k8s_file="$K8S_CONFIG"
    local version=""
    local create_backup=true
    local apply_changes=false
    local show_verification=false
    
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -f|--file)
                k8s_file="$2"
                shift 2
                ;;
            -n|--no-backup)
                create_backup=false
                shift
                ;;
            -a|--apply)
                apply_changes=true
                shift
                ;;
            -v|--verify)
                show_verification=true
                shift
                ;;
            -h|--help)
                show_usage
                exit 0
                ;;
            -*)
                print_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
            *)
                if [[ -z "$version" ]]; then
                    version="$1"
                else
                    print_error "Multiple versions specified: '$version' and '$1'"
                    show_usage
                    exit 1
                fi
                shift
                ;;
        esac
    done
    
    # Check if version is provided
    if [[ -z "$version" ]]; then
        print_error "Version is required"
        show_usage
        exit 1
    fi
    
    # Resolve absolute path for k8s file
    k8s_file=$(realpath "$k8s_file" 2>/dev/null || echo "$k8s_file")
    
    print_info "========================================"
    print_info "   Kubernetes Image Update             "
    print_info "========================================"
    print_info "File:    $k8s_file"
    print_info "Version: $version"
    echo
    
    validate_version "$version"
    check_prerequisites "$k8s_file"
    
    if [[ $show_verification == true ]]; then
        show_current_versions "$k8s_file"
        echo
    fi
    
    if [[ $create_backup == true ]]; then
        create_backup "$k8s_file"
    fi
    
    if update_images "$k8s_file" "$version"; then
        print_success "✓ Image versions updated successfully"
        
        if [[ $show_verification == true ]]; then
            echo
            print_info "Updated versions:"
            grep -E "image: (searchapi|searchweb):" "$k8s_file" | sed 's/^/  /'
        fi
        
        if [[ $apply_changes == true ]]; then
            echo
            apply_to_kubernetes "$k8s_file"
        else
            show_next_steps "$k8s_file" "$version"
        fi
    else
        print_error "Failed to update image versions"
        exit 1
    fi
}

# Execute main function
main "$@"