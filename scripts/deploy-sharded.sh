#!/bin/bash

# SearchEngine Sharded PostgreSQL Setup Script for Minikube
# This script sets up the sharded database architecture with 3 PostgreSQL instances

set -e

echo "🚀 Starting SearchEngine Sharded PostgreSQL Setup..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if minikube is running
echo -e "${BLUE}Checking Minikube status...${NC}"
if ! minikube status > /dev/null 2>&1; then
    echo -e "${YELLOW}Starting Minikube...${NC}"
    minikube start
else
    echo -e "${GREEN}Minikube is already running${NC}"
fi

# Set docker environment
echo -e "${BLUE}Setting up Docker environment for Minikube...${NC}"
eval $(minikube docker-env)

# Build Docker images
echo -e "${BLUE}Building Docker images...${NC}"

echo "Building SearchAPI..."
docker build -t searchapi:latest -f SearchAPI/dockerfile .

echo "Building SearchWeb..."
docker build -t searchweb:latest -f SearchWeb/dockerfile .

echo "Building Indexer..."
docker build -t indexer:latest -f indexer/Dockerfile .

# Apply Kubernetes configuration
echo -e "${BLUE}Deploying to Kubernetes...${NC}"
kubectl apply -f searchengine-sharded.yaml

echo -e "${BLUE}Waiting for databases to be ready...${NC}"
kubectl wait --for=condition=ready pod -l app=postgres-words -n searchengine --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres-documents -n searchengine --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres-occurrences -n searchengine --timeout=300s

echo -e "${GREEN}✅ All PostgreSQL shards are ready!${NC}"

# Wait for SearchAPI to be ready
echo -e "${BLUE}Waiting for SearchAPI to be ready...${NC}"
kubectl wait --for=condition=ready pod -l app=searchapi -n searchengine --timeout=300s

echo -e "${GREEN}✅ SearchAPI is ready!${NC}"

# Wait for SearchWeb to be ready
echo -e "${BLUE}Waiting for SearchWeb to be ready...${NC}"
kubectl wait --for=condition=ready pod -l app=searchweb -n searchengine --timeout=300s

echo -e "${GREEN}✅ SearchWeb is ready!${NC}"

# Get service URLs
echo -e "${BLUE}Getting service information...${NC}"
SEARCHWEB_URL=$(minikube service searchweb -n searchengine --url)
SEARCHAPI_URL=$(minikube service searchapi -n searchengine --url)

echo -e "${GREEN}"
echo "=========================================="
echo "🎉 SearchEngine Deployment Complete!"
echo "=========================================="
echo "SearchWeb Frontend: $SEARCHWEB_URL"
echo "SearchAPI Backend: $SEARCHAPI_URL"
echo ""
echo "Database Shards Status:"
echo "- Words DB: $(kubectl get pod -l app=postgres-words -n searchengine -o jsonpath='{.items[0].status.phase}')"
echo "- Documents DB: $(kubectl get pod -l app=postgres-documents -n searchengine -o jsonpath='{.items[0].status.phase}')"
echo "- Occurrences DB: $(kubectl get pod -l app=postgres-occurrences -n searchengine -o jsonpath='{.items[0].status.phase}')"
echo ""
echo "To check database health:"
echo "curl $SEARCHAPI_URL/api/health"
echo ""
echo "To run the indexer job:"
echo "kubectl create job indexer-manual --from=cronjob/indexer-job -n searchengine"
echo "=========================================="
echo -e "${NC}"

# Optionally open the web interface
read -p "Do you want to open the SearchWeb interface in your browser? (y/n): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${BLUE}Opening SearchWeb...${NC}"
    open "$SEARCHWEB_URL" || echo "Please manually open: $SEARCHWEB_URL"
fi