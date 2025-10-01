#!/bin/bash

# Comprehensive startup script for SearchEngine project
# This script starts all services in the correct order

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}   Starting SearchEngine Services      ${NC}"
echo -e "${BLUE}========================================${NC}"

# Function to start a service in a new terminal
start_service() {
    local service_name=$1
    local service_path=$2
    local run_command=$3
    local port=$4
    
    echo -e "${YELLOW}Starting $service_name on port $port...${NC}"
    
    # Create an AppleScript to open a new Terminal window and run the command
    osascript -e "tell application \"Terminal\" to do script \"cd '$service_path' && echo 'Starting $service_name...' && $run_command\""
    
    # Give it a moment to start
    sleep 2
}

# Function to check if a service is responding
check_service() {
    local service_name=$1
    local url=$2
    local max_attempts=30
    local attempt=1
    
    echo -e "${YELLOW}Waiting for $service_name to be ready...${NC}"
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s "$url" > /dev/null 2>&1; then
            echo -e "${GREEN}✓ $service_name is ready!${NC}"
            return 0
        fi
        
        echo -n "."
        sleep 1
        ((attempt++))
    done
    
    echo -e "${RED}✗ $service_name failed to start after $max_attempts seconds${NC}"
    return 1
}

# Start services in order
echo -e "${BLUE}1. Starting SearchAPI instances...${NC}"
start_service "SearchAPI Instance 1" "$(pwd)/SearchAPI" "dotnet run --launch-profile http" "5154"
start_service "SearchAPI Instance 2" "$(pwd)/SearchAPI" "dotnet run --launch-profile http2" "5155"

# Wait for SearchAPI instances to be ready
check_service "SearchAPI Instance 1" "http://localhost:5154/api/ping"
check_service "SearchAPI Instance 2" "http://localhost:5155/api/ping"

echo -e "${BLUE}2. Starting LoadBalancer...${NC}"
start_service "LoadBalancer API" "$(pwd)/LoadBalancerAPI" "dotnet run" "5156"

# Wait for LoadBalancer to be ready
check_service "LoadBalancer API" "http://localhost:5156/health"

echo -e "${BLUE}3. Starting SearchWeb...${NC}"
start_service "SearchWeb" "$(pwd)/SearchWeb" "dotnet run" "5068"

# Wait for SearchWeb to be ready
check_service "SearchWeb" "http://localhost:5068"

echo -e "${BLUE}4. Starting ConsoleSearch...${NC}"
start_service "ConsoleSearch" "$(pwd)/ConsoleSearch" "dotnet run" "N/A"

echo
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}   All Services Started Successfully!  ${NC}"
echo -e "${GREEN}========================================${NC}"
echo
echo -e "${YELLOW}Service URLs:${NC}"
echo -e "  • SearchAPI Instance 1: ${BLUE}http://localhost:5154${NC}"
echo -e "  • SearchAPI Instance 2: ${BLUE}http://localhost:5155${NC}"
echo -e "  • LoadBalancer API:     ${BLUE}http://localhost:5156${NC}"
echo -e "  • SearchWeb:            ${BLUE}http://localhost:5068${NC}"
echo -e "  • ConsoleSearch:        ${YELLOW}Running in terminal${NC}"
echo
echo -e "${YELLOW}Test Commands:${NC}"
echo -e "  • Health Check:         ${BLUE}curl http://localhost:5156/health${NC}"
echo -e "  • Load Balanced Ping:   ${BLUE}curl http://localhost:5156/api/ping${NC}"
echo -e "  • Load Balanced Search: ${BLUE}curl http://localhost:5156/api/search/test/5${NC}"
echo -e "  • Web Interface:        ${BLUE}open http://localhost:5068${NC}"
echo
echo -e "${GREEN}Press any key to run basic tests...${NC}"
read -n 1 -s

echo -e "${BLUE}Running basic tests...${NC}"

# Test LoadBalancer health
echo -e "${YELLOW}1. Testing LoadBalancer health:${NC}"
curl -s http://localhost:5156/health | jq '.' 2>/dev/null || curl -s http://localhost:5156/health

# Test load balanced ping (show round-robin)
echo -e "\n${YELLOW}2. Testing round-robin load balancing:${NC}"
for i in {1..4}; do
    echo -e "\nPing $i:"
    curl -s http://localhost:5156/api/ping
done

# Open web interface
echo -e "\n${YELLOW}3. Opening web interface...${NC}"
open http://localhost:5068

echo -e "\n${GREEN}Setup complete! All services are running.${NC}"
echo -e "${YELLOW}To stop all services, close the terminal windows or press Ctrl+C in each.${NC}"