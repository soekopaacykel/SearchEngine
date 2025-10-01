#!/bin/bash

# Test script for Load Balancer API
# Make sure all services are running before executing this script

echo "Testing Load Balancer API..."
echo "================================="

# Test health endpoint
echo -e "\n1. Checking health status:"
curl -s http://localhost:5156/health | jq '.' 2>/dev/null || curl -s http://localhost:5156/health

# Test ping endpoint multiple times to see round-robin
echo -e "\n\n2. Testing ping endpoint (round-robin):"
for i in {1..4}; do
    echo -e "\nPing $i:"
    curl -s http://localhost:5156/api/ping
done

# Test search endpoint
echo -e "\n\n3. Testing search endpoint:"
curl -s http://localhost:5156/api/search/test,query/5 | jq '.' 2>/dev/null || curl -s http://localhost:5156/api/search/test,query/5

echo -e "\n\nTest completed!"
echo -e "\nTo run manually:"
echo "Health: curl http://localhost:5156/health"
echo "Ping: curl http://localhost:5156/api/ping"
echo "Search: curl http://localhost:5156/api/search/test,query/5"