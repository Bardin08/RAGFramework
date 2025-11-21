# Versioning Strategy

This document describes the versioning strategy for the RAG Framework.

## Overview

The RAG Framework uses **Semantic Versioning (SemVer)** with automatic version bumping based on conventional commits.

## Version Format

```
v{MAJOR}.{MINOR}.{PATCH}-{SHORT-SHA}
```

**Examples:**
- `v1.2.3-abc1234`
- `v2.0.0-def5678`
- `v1.5.12-9a8b7c6`

### Components

- **MAJOR**: Incremented for breaking changes
- **MINOR**: Incremented for new features (backwards compatible)
- **PATCH**: Incremented for bug fixes and minor changes
- **SHORT-SHA**: First 7 characters of the git commit SHA

## Image Tagging Strategy

Each Docker image is tagged with **3 tags**:

1. **`latest`** - Always points to the most recent build
2. **`v{major}.{minor}-{short-sha}`** - Minor version tag
3. **`v{major}.{minor}.{patch}-{short-sha}`** - Full semver tag

**Example for version v1.2.3 with commit abc1234:**
```
ghcr.io/bardin08/ragframework/ragcore-api:latest
ghcr.io/bardin08/ragframework/ragcore-api:v1.2-abc1234
ghcr.io/bardin08/ragframework/ragcore-api:v1.2.3-abc1234
```

## Automatic Version Bumping

Version bumps are determined by **Conventional Commits** in the commit messages.

### Commit Message Convention

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Version Bump Rules

| Commit Pattern | Version Bump | Example |
|---------------|--------------|---------|
| `feat!:` or `BREAKING CHANGE:` | **MAJOR** (v1.0.0 → v2.0.0) | `feat!: redesign API endpoints` |
| `feat:` | **MINOR** (v1.0.0 → v1.1.0) | `feat: add new search algorithm` |
| `fix:`, `chore:`, `docs:`, etc. | **PATCH** (v1.0.0 → v1.0.1) | `fix: resolve memory leak` |

### Examples

**Major Version Bump:**
```bash
git commit -m "feat!: redesign API with breaking changes"
# or
git commit -m "feat: major refactor

BREAKING CHANGE: API endpoints have changed"
```
Result: `v1.5.3` → `v2.0.0`

**Minor Version Bump:**
```bash
git commit -m "feat: add hybrid search with RRF reranking"
```
Result: `v1.5.3` → `v1.6.0`

**Patch Version Bump:**
```bash
git commit -m "fix: correct connection string parsing"
# or
git commit -m "chore: update dependencies"
```
Result: `v1.5.3` → `v1.5.4`

## Workflow

### 1. Development

Developers work on feature branches:
```bash
git checkout -b feat/hybrid-search
# Make changes
git commit -m "feat: implement hybrid search"
git push origin feat/hybrid-search
```

### 2. Pull Request

Create PR to `main` branch. CI runs:
- Build validation
- Tests
- Docker image build (no push)
- Security scanning

### 3. Merge to Main

When PR is merged to `main`:

**Step 1:** `create-release.yml` workflow runs:
- Analyzes commit messages since last tag
- Determines version bump type (major/minor/patch)
- Creates new git tag (e.g., `v1.2.3`)
- Pushes tag to repository

**Step 2:** `build-images.yml` workflow triggers on tag push:
- Builds all 3 Docker images (API, Embedding, Migrations)
- Tags images with 3 tags: `latest`, `v1.2-abc1234`, `v1.2.3-abc1234`
- Pushes images to GHCR
- Creates GitHub Release with changelog

### 4. Deployment

In **gitops repository**, update the image tag:
```yaml
global:
  imageTag: "v1.2.3-abc1234"
```

ArgoCD syncs the change:
- Runs migration job (PreSync)
- Rolling updates API and Embedding services

## Manual Version Control

### Rebuild Existing Tag

Use workflow dispatch to rebuild images for an existing tag:

```bash
# Via GitHub UI: Actions → Build and Push Images → Run workflow
# Input: v1.2.3
```

Or via GitHub CLI:
```bash
gh workflow run build-images.yml -f tag=v1.2.3
```

### Create Custom Tag

If automatic versioning doesn't meet your needs:

```bash
# Create and push custom tag manually
git tag -a v1.5.0 -m "Release v1.5.0 - Custom release"
git push origin v1.5.0

# build-images.yml will automatically trigger
```

## Version Synchronization

**Important:** All 3 services (API, Embedding, Migrations) use the **same version tag**.

This ensures:
- ✅ Compatibility between services
- ✅ Easy rollback (single tag to revert)
- ✅ Clear deployment state

Even if only the API changed, all images are rebuilt and tagged together.

## Tag Management

### List Tags

```bash
# Local tags
git tag

# Remote tags
git ls-remote --tags origin

# Latest tag
git describe --tags --abbrev=0
```

### Delete Tag

**Use with caution!** Deleting tags can cause confusion.

```bash
# Delete local tag
git tag -d v1.2.3

# Delete remote tag
git push origin :refs/tags/v1.2.3
```

## Best Practices

### DO:
✅ Use conventional commits for all changes
✅ Write clear commit messages describing "why"
✅ Test thoroughly before merging to main
✅ Document breaking changes in commit messages
✅ Use feature flags for gradual rollouts

### DON'T:
❌ Manually edit version numbers in code
❌ Push directly to main (use PRs)
❌ Create tags manually unless necessary
❌ Delete tags after images are published
❌ Use `latest` tag in production (use specific versions)

## Example Workflow

```bash
# 1. Start new feature
git checkout -b feat/add-caching

# 2. Implement feature
# ... code changes ...

# 3. Commit with conventional message
git add .
git commit -m "feat: add Redis caching layer

Implements caching for frequently accessed documents
to improve response time by ~40%."

# 4. Push and create PR
git push origin feat/add-caching

# 5. After PR approval and merge to main:
#    - create-release.yml creates v1.6.0 tag
#    - build-images.yml builds and pushes images

# 6. In gitops repo, update values:
global:
  imageTag: "v1.6.0-abc1234"

# 7. ArgoCD syncs and deploys new version
```

## Hotfix Workflow

For urgent production fixes:

```bash
# 1. Create hotfix branch from latest release tag
git checkout -b hotfix/security-fix v1.5.3

# 2. Apply fix
# ... fix critical issue ...

# 3. Commit with appropriate message
git commit -m "fix: patch XSS vulnerability in search endpoint

CVE-2024-12345: Sanitize user input in query parameters"

# 4. Merge to main
git checkout main
git merge hotfix/security-fix

# 5. Automatic workflow creates v1.5.4 tag and builds images

# 6. Deploy immediately via gitops
```

## Rollback

To rollback to a previous version:

```bash
# In gitops repository
global:
  imageTag: "v1.5.2-xyz9876"  # Previous stable version

# ArgoCD syncs and rolls back
```

## Version History

Check version history:

```bash
# GitHub Releases
https://github.com/Bardin08/RAGFramework/releases

# Git tags with commits
git log --tags --simplify-by-decoration --pretty="format:%ai %d"

# Helm release history (in cluster)
helm history rag-framework -n rag-framework
```

---

For more information:
- [Conventional Commits](https://www.conventionalcommits.org/)
- [Semantic Versioning](https://semver.org/)
- [Deployment Guide](./README.md)
