#!/bin/bash

# Cleanup Script for SearchEngine
# This script removes all SearchEngine resources from Minikube

echo "🧹 Cleaning up SearchEngine deployment..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Remove all resources in the searchengine namespace
echo -e "${BLUE}Removing SearchEngine resources...${NC}"

if kubectl get namespace searchengine > /dev/null 2>&1; then
    # Delete all resources in namespace
    kubectl delete namespace searchengine --grace-period=30
    
    echo -e "${YELLOW}Waiting for namespace to be fully deleted...${NC}"
    kubectl wait --for=delete namespace/searchengine --timeout=60s || true
    
    echo -e "${GREEN}✅ SearchEngine resources cleaned up${NC}"
else
    echo -e "${YELLOW}⚠️  SearchEngine namespace does not exist${NC}"
fi

# Optional: Clean up Docker images
read -p "Do you want to clean up Docker images as well? (y/n): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${BLUE}Cleaning up Docker images...${NC}"
    eval $(minikube docker-env)
    docker rmi searchapi:latest searchweb:latest indexer:latest 2>/dev/null || true
    echo -e "${GREEN}✅ Docker images cleaned up${NC}"
fi

echo -e "${GREEN}Cleanup complete!${NC}"