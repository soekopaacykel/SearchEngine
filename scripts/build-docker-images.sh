#!/bin/bash

# Docker Image Build Script with Versioning
# This script builds SearchAPI and SearchWeb Docker images with version tags

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
readonly VERSION="${1:-${DOCKER_VERSION:-"1.0.0"}}"

# Function to print colored messages
print_info() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}$1${NC}"; }
print_warning() { echo -e "${YELLOW}$1${NC}"; }
print_error() { echo -e "${RED}$1${NC}"; }

# Function to print header
print_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}   Building Docker Images v${VERSION}   ${NC}"
    echo -e "${BLUE}========================================${NC}"
}

# Function to check if Docker is running
check_docker() {
    if ! docker info >/dev/null 2>&1; then
        print_error "Docker is not running. Please start Docker and try again."
        exit 1
    fi
}

# Function to check if required files exist
check_prerequisites() {
    local search_api_dockerfile="${PROJECT_ROOT}/SearchAPI/dockerfile"
    local search_web_dockerfile="${PROJECT_ROOT}/SearchWeb/dockerfile"
    
    if [[ ! -f "$search_api_dockerfile" ]]; then
        print_error "SearchAPI dockerfile not found at: $search_api_dockerfile"
        exit 1
    fi
    
    if [[ ! -f "$search_web_dockerfile" ]]; then
        print_error "SearchWeb dockerfile not found at: $search_web_dockerfile"
        exit 1
    fi
}

# Function to build and tag Docker image
build_image() {
    local service_name="$1"
    local dockerfile_path="$2"
    local context_path="$3"
    
    print_info "Building ${service_name}..."
    
    # Build the image with version tag
    if docker build -t "${service_name}:${VERSION}" -f "${dockerfile_path}" "${context_path}"; then
        print_success "✓ ${service_name}:${VERSION} built successfully"
        
        # Also tag as latest
        docker tag "${service_name}:${VERSION}" "${service_name}:latest"
        print_success "✓ ${service_name}:latest tag created"
        return 0
    else
        print_error "✗ Failed to build ${service_name}"
        return 1
    fi
}

# Function to display usage
show_usage() {
    echo "Usage: $0 [VERSION]"
    echo ""
    echo "Arguments:"
    echo "  VERSION    Version tag for the Docker images (default: 1.0.0)"
    echo ""
    echo "Examples:"
    echo "  $0                # Uses default version 1.0.0"
    echo "  $0 2.1.0         # Uses version 2.1.0"
    echo ""
    echo "Environment Variables:"
    echo "  DOCKER_VERSION   Default version if not specified as argument"
}

# Function to display built images summary
show_summary() {
    echo
    print_success "========================================"
    print_success "   Build Complete!                     "
    print_success "========================================"
    echo
    
    print_warning "Built Images:"
    echo -e "  • searchapi:${VERSION}"
    echo -e "  • searchapi:latest"
    echo -e "  • searchweb:${VERSION}"
    echo -e "  • searchweb:latest"
    
    echo
    print_warning "Available Commands:"
    echo -e "  • List images:          ${BLUE}docker images | grep -E '(searchapi|searchweb)'${NC}"
    echo -e "  • Update Kubernetes:    ${BLUE}./scripts/update-k8s-images.sh ${VERSION}${NC}"
    echo -e "  • Check image sizes:    ${BLUE}docker images --format 'table {{.Repository}}:{{.Tag}}\t{{.Size}}' | grep -E '(searchapi|searchweb)'${NC}"
    
    echo
    print_info "To use in Kubernetes:"
    echo -e "  ${BLUE}./scripts/update-k8s-images.sh ${VERSION}${NC}"
    echo -e "  ${BLUE}kubectl apply -f searchengine-minikube.yaml${NC}"
}

# Main execution
main() {
    # Check for help flag
    if [[ "${1:-}" == "-h" ]] || [[ "${1:-}" == "--help" ]]; then
        show_usage
        exit 0
    fi
    
    # Change to project root directory
    cd "${PROJECT_ROOT}"
    
    print_header
    check_docker
    check_prerequisites
    
    print_info "Working directory: ${PROJECT_ROOT}"
    print_info "Using version: ${VERSION}"
    echo
    
    # Build SearchAPI
    print_info "1. Building SearchAPI..."
    if ! build_image "searchapi" "SearchAPI/dockerfile" "."; then
        print_error "Failed to build SearchAPI. Aborting."
        exit 1
    fi
    
    echo
    
    # Build SearchWeb
    print_info "2. Building SearchWeb..."
    if ! build_image "searchweb" "SearchWeb/dockerfile" "."; then
        print_error "Failed to build SearchWeb. Aborting."
        exit 1
    fi
    
    show_summary
}

# Execute main function
main "$@"