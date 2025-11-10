# SearchEngine with Sharded PostgreSQL - Deployment Guide

This guide shows how to deploy the SearchEngine application with a sharded PostgreSQL architecture using 3 separate database instances.

## Architecture Overview

The sharded architecture consists of:

- **Words Database** (`postgres-words`) - Stores the `word` table
- **Documents Database** (`postgres-documents`) - Stores the `document` table  
- **Occurrences Database** (`postgres-occurrences`) - Stores the `occ` table

This horizontal sharding approach allows each database to be scaled independently based on load.

## Prerequisites

1. **Minikube** installed and running
2. **kubectl** configured to work with Minikube
3. **Docker** (for building images)
4. **jq** (optional, for JSON formatting in health checks)

## Quick Start

### 1. Deploy Everything

Run the automated deployment script:

```bash
./scripts/deploy-sharded.sh
```

This script will:
- Start Minikube (if not running)
- Build all Docker images
- Deploy the 3 PostgreSQL shards
- Deploy SearchAPI and SearchWeb
- Wait for all services to be ready
- Display service URLs

### 2. Check Health

Check the status of all database shards:

```bash
./scripts/check-health.sh
```

### 3. Access the Application

After deployment, you'll get URLs like:
- **SearchWeb Frontend**: http://192.168.49.2:30080
- **SearchAPI Backend**: http://192.168.49.2:XXXXX

### 4. Cleanup (when done)

Remove all resources:

```bash
./scripts/cleanup.sh
```

## Manual Deployment Steps

If you prefer to deploy manually:

### 1. Build Docker Images

```bash
# Set Minikube Docker environment
eval $(minikube docker-env)

# Build images
cd SearchAPI && docker build -t searchapi:latest .
cd ../SearchWeb && docker build -t searchweb:latest .
cd ../indexer && docker build -t indexer:latest .
```

### 2. Deploy to Kubernetes

```bash
kubectl apply -f searchengine-sharded.yaml
```

### 3. Wait for Services

```bash
# Wait for databases
kubectl wait --for=condition=ready pod -l app=postgres-words -n searchengine --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres-documents -n searchengine --timeout=300s
kubectl wait --for=condition=ready pod -l app=postgres-occurrences -n searchengine --timeout=300s

# Wait for applications
kubectl wait --for=condition=ready pod -l app=searchapi -n searchengine --timeout=300s
kubectl wait --for=condition=ready pod -l app=searchweb -n searchengine --timeout=300s
```

## Monitoring and Troubleshooting

### Check Pod Status

```bash
kubectl get pods -n searchengine
```

### Check Database Health via API

```bash
# Get SearchAPI URL
SEARCHAPI_URL=$(minikube service searchapi -n searchengine --url)

# Check database health
curl $SEARCHAPI_URL/api/health | jq '.'
```

### View Logs

```bash
# Database logs
kubectl logs -l app=postgres-words -n searchengine
kubectl logs -l app=postgres-documents -n searchengine
kubectl logs -l app=postgres-occurrences -n searchengine

# Application logs
kubectl logs -l app=searchapi -n searchengine
kubectl logs -l app=searchweb -n searchengine
```

### Connect to Databases Directly

```bash
# Connect to Words database
kubectl exec -it deployment/postgres-words -n searchengine -- psql -U ameliavalentin -d search_words

# Connect to Documents database
kubectl exec -it deployment/postgres-documents -n searchengine -- psql -U ameliavalentin -d search_documents

# Connect to Occurrences database
kubectl exec -it deployment/postgres-occurrences -n searchengine -- psql -U ameliavalentin -d search_occurrences
```

## Database Schema

Each shard has its own optimized schema:

### Words Database (`search_words`)
```sql
CREATE TABLE word(
    id INTEGER PRIMARY KEY, 
    name TEXT UNIQUE
);
CREATE INDEX word_name_index ON word (name);
```

### Documents Database (`search_documents`)
```sql
CREATE TABLE document(
    id INTEGER PRIMARY KEY, 
    url TEXT, 
    idxTime TEXT, 
    creationTime TEXT
);
CREATE INDEX document_url_index ON document (url);
```

### Occurrences Database (`search_occurrences`)
```sql
CREATE TABLE occ(
    wordId INTEGER, 
    docId INTEGER
);
CREATE INDEX word_index ON occ (wordId);
CREATE INDEX doc_index ON occ (docId);
```

## Running the Indexer

To populate the databases with data:

```bash
# This will run the indexer as a Kubernetes job
kubectl create job indexer-manual --from=cronjob/indexer-job -n searchengine

# Check indexer progress
kubectl logs job/indexer-manual -n searchengine -f
```

## Scaling Individual Shards

You can scale individual database shards based on load:

```bash
# Scale words database (if it gets heavy read traffic)
kubectl scale deployment postgres-words --replicas=2 -n searchengine

# Scale documents database
kubectl scale deployment postgres-documents --replicas=2 -n searchengine

# Scale occurrences database (likely the most accessed)
kubectl scale deployment postgres-occurrences --replicas=3 -n searchengine
```

## Features

- ✅ **Horizontal Database Sharding** - 3 separate PostgreSQL instances
- ✅ **Health Monitoring** - API endpoint for database health checks
- ✅ **Web Interface** - Real-time database status display
- ✅ **Automatic Schema Setup** - Databases initialized with proper schemas
- ✅ **Load Balancing** - Round-robin connection management
- ✅ **Fault Tolerance** - Graceful handling of database failures
- ✅ **Kubernetes Native** - Full container orchestration support

## Troubleshooting

### Common Issues

1. **Databases not ready**: Wait longer for initialization or check logs
2. **Connection refused**: Ensure all services are running and healthy
3. **Image pull errors**: Make sure you're using Minikube's Docker environment

### Getting Help

Check the logs and health status:
```bash
./scripts/check-health.sh
kubectl get events -n searchengine --sort-by=.lastTimestamp
```