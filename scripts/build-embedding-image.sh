#!/bin/bash
set -e

# Manual script to build and push the embedding service Docker image
# This image is ~3GB and excluded from CI/CD to save time and resources

REGISTRY="ghcr.io"
REGISTRY_PATH="ghcr.io/bardin08/ragframework"
IMAGE_NAME="ragcore-embedding"

# Check if tag is provided
if [ -z "$1" ]; then
    echo "Usage: $0 <version-tag> [short-sha]"
    echo "Example: $0 v0.6.0 8669ae4"
    exit 1
fi

VERSION_TAG=$1
SHORT_SHA=${2:-$(git rev-parse --short=7 HEAD)}

# Extract version components
VERSION=${VERSION_TAG#v}
IFS='.' read -r MAJOR MINOR PATCH <<< "$VERSION"

echo "Building embedding service image..."
echo "Version: $VERSION_TAG"
echo "Short SHA: $SHORT_SHA"
echo ""

# Build image tags
TAGS=(
    "$REGISTRY_PATH/$IMAGE_NAME:latest"
    "$REGISTRY_PATH/$IMAGE_NAME:$MAJOR.$MINOR-$SHORT_SHA"
    "$REGISTRY_PATH/$IMAGE_NAME:$MAJOR.$MINOR.$PATCH-$SHORT_SHA"
)

# Build tag arguments
TAG_ARGS=""
for tag in "${TAGS[@]}"; do
    TAG_ARGS="$TAG_ARGS -t $tag"
done

# Build the image
echo "Building image with tags:"
for tag in "${TAGS[@]}"; do
    echo "  - $tag"
done
echo ""

docker build \
    --platform linux/amd64 \
    $TAG_ARGS \
    -f python-services/embedding-service/Dockerfile \
    python-services/embedding-service

echo ""
echo "Build completed successfully!"
echo ""
echo "To push to registry, run:"
echo "  docker login $REGISTRY"
for tag in "${TAGS[@]}"; do
    echo "  docker push $tag"
done
echo ""
echo "Or push all at once:"
echo "  for tag in ${TAGS[@]}; do docker push \$tag; done"
