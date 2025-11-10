#!/bin/bash

# Database Health Check Script
# This script checks the health of all PostgreSQL shards

echo "🔍 Checking PostgreSQL Shards Health..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to check database health
check_db_health() {
    local db_name=$1
    local namespace="searchengine"
    
    echo -e "${BLUE}Checking $db_name...${NC}"
    
    # Check if pod is running
    if kubectl get pod -l app=$db_name -n $namespace | grep -q "Running"; then
        echo -e "${GREEN}✅ $db_name pod is running${NC}"
        
        # Check database connectivity
        if kubectl exec -n $namespace deployment/$db_name -- pg_isready -U ameliavalentin > /dev/null 2>&1; then
            echo -e "${GREEN}✅ $db_name database is responding${NC}"
        else
            echo -e "${RED}❌ $db_name database is not responding${NC}"
        fi
    else
        echo -e "${RED}❌ $db_name pod is not running${NC}"
    fi
    echo ""
}

# Check namespace exists
if ! kubectl get namespace searchengine > /dev/null 2>&1; then
    echo -e "${RED}❌ Namespace 'searchengine' does not exist. Please run the deployment script first.${NC}"
    exit 1
fi

# Check each database
check_db_health "postgres-words"
check_db_health "postgres-documents" 
check_db_health "postgres-occurrences"

# Check SearchAPI health if available
echo -e "${BLUE}Checking SearchAPI health...${NC}"
SEARCHAPI_URL=$(minikube service searchapi -n searchengine --url 2>/dev/null)
if [ ! -z "$SEARCHAPI_URL" ]; then
    echo "SearchAPI URL: $SEARCHAPI_URL"
    if curl -s "$SEARCHAPI_URL/api/health" > /dev/null 2>&1; then
        echo -e "${GREEN}✅ SearchAPI health endpoint is accessible${NC}"
        echo "Database shards status from API:"
        curl -s "$SEARCHAPI_URL/api/health" | jq '.' 2>/dev/null || curl -s "$SEARCHAPI_URL/api/health"
    else
        echo -e "${YELLOW}⚠️  SearchAPI health endpoint is not yet accessible${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  SearchAPI service not yet available${NC}"
fi

echo ""
echo -e "${BLUE}To get more detailed logs:${NC}"
echo "kubectl logs -l app=postgres-words -n searchengine"
echo "kubectl logs -l app=postgres-documents -n searchengine" 
echo "kubectl logs -l app=postgres-occurrences -n searchengine"
echo "kubectl logs -l app=searchapi -n searchengine"