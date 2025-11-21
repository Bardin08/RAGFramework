#!/bin/bash

# Test script for Docker builds
# Run from repository root: ./test-docker-builds.sh

set -e

echo "====================================="
echo "Testing Docker Builds"
echo "====================================="
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Test 1: API
echo "1. Building API image..."
if docker build -f src/RAG.API/Dockerfile -t ragcore-api:test . ; then
    echo -e "${GREEN}✓ API build successful${NC}"
else
    echo -e "${RED}✗ API build failed${NC}"
    exit 1
fi
echo ""

# Test 2: Embedding
echo "2. Building Embedding image..."
if docker build -f python-services/embedding-service/Dockerfile -t ragcore-embedding:test ./python-services/embedding-service ; then
    echo -e "${GREEN}✓ Embedding build successful${NC}"
else
    echo -e "${RED}✗ Embedding build failed${NC}"
    exit 1
fi
echo ""

# Test 3: Migrations (Note: Will work once EF migrations are added)
echo "3. Building Migrations image..."
echo "   Note: This will fail if no EF migrations exist yet"
if docker build -f src/RAG.Infrastructure/Migrations.Dockerfile -t ragcore-migrations:test . ; then
    echo -e "${GREEN}✓ Migrations build successful${NC}"
else
    echo -e "${RED}⚠ Migrations build failed (expected if no EF migrations yet)${NC}"
fi
echo ""

# List built images
echo "====================================="
echo "Built Images:"
echo "====================================="
docker images | grep ragcore

echo ""
echo "====================================="
echo "Build Tests Complete!"
echo "====================================="
