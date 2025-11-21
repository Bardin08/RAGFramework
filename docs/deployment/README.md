# RAG Framework Deployment Guide

This guide explains how to deploy the RAG Framework to a Kubernetes cluster using Helm.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Deployment Steps](#deployment-steps)
- [Verification](#verification)
- [Troubleshooting](#troubleshooting)

## Prerequisites

- Kubernetes cluster (k3s, k8s, or equivalent) version 1.24+
- `kubectl` configured to access your cluster
- `helm` v3.8+ installed
- GitHub PAT with `packages:read` permission (for private images)
- Access to the gitops repository for secrets management

## Quick Start

### 1. Set Image Tag

In your gitops repository, set the desired image tag:

```yaml
# values.yaml or values override
global:
  imageTag: "v1.2.3-abc1234"  # Replace with your desired tag
```

### 2. Configure Secrets

Create the required secrets in your gitops repository (using SealedSecrets, External Secrets Operator, or similar):

**PostgreSQL Connection:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: postgres-connection
  namespace: <your-namespace>
type: Opaque
stringData:
  connection-string: "Host=<postgres-host>;Port=5432;Database=rag_db;Username=<user>;Password=<password>"
```

**PostgreSQL Credentials:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: postgres-credentials
  namespace: <your-namespace>
type: Opaque
stringData:
  username: "<postgres-user>"
  password: "<postgres-password>"
```

**Keycloak Credentials:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: keycloak-credentials
  namespace: <your-namespace>
type: Opaque
stringData:
  admin-username: "admin"
  admin-password: "<secure-password>"
```

**MinIO Credentials:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: minio-credentials
  namespace: <your-namespace>
type: Opaque
stringData:
  root-user: "minioadmin"
  root-password: "<secure-password>"
```

**GitHub Container Registry (for private images):**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ghcr-credentials
  namespace: <your-namespace>
type: kubernetes.io/dockerconfigjson
data:
  .dockerconfigjson: <base64-encoded-docker-config>
```

Generate `.dockerconfigjson`:
```bash
kubectl create secret docker-registry ghcr-credentials \
  --docker-server=ghcr.io \
  --docker-username=<github-username> \
  --docker-password=<github-pat> \
  --docker-email=<email> \
  --dry-run=client -o yaml
```

### 3. Deploy via ArgoCD (App of Apps)

Reference this Helm chart in your ArgoCD application:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: rag-framework
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/Bardin08/RAGFramework
    targetRevision: main
    path: helm
    helm:
      values: |
        global:
          imageTag: "v1.2.3-abc1234"

        image:
          pullSecrets:
            - name: ghcr-credentials

        # Enable/disable migrations
        migrations:
          enabled: true

        # Customize resources if needed
        api:
          replicaCount: 2

        embedding:
          replicaCount: 2

        # ... other overrides
  destination:
    server: https://kubernetes.default.svc
    namespace: rag-framework
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

### 4. Manual Deployment (without ArgoCD)

```bash
# Create namespace
kubectl create namespace rag-framework

# Apply secrets first (from your gitops repo)
kubectl apply -f secrets/ -n rag-framework

# Install/Upgrade Helm release
helm upgrade --install rag-framework ./helm \
  --namespace rag-framework \
  --set global.imageTag=v1.2.3-abc1234 \
  --set image.pullSecrets[0].name=ghcr-credentials
```

## Configuration

### Key Configuration Options

| Parameter | Description | Default |
|-----------|-------------|---------|
| `global.imageTag` | Image tag for all services | `""` (required) |
| `image.pullSecrets` | Image pull secrets | `[]` |
| `api.replicaCount` | Number of API replicas | `2` |
| `embedding.replicaCount` | Number of embedding service replicas | `2` |
| `migrations.enabled` | Enable automatic migrations | `true` |
| `postgres.enabled` | Deploy PostgreSQL | `true` |
| `postgres.persistence.size` | PostgreSQL storage size | `10Gi` |
| `elasticsearch.enabled` | Deploy Elasticsearch | `true` |
| `elasticsearch.persistence.size` | Elasticsearch storage size | `20Gi` |
| `qdrant.enabled` | Deploy Qdrant | `true` |
| `ingress.enabled` | Enable ingress | `false` |

See `helm/values.yaml` for complete configuration options.

### Resource Allocation

Default resource requests/limits are defined for all services. Adjust in your gitops overrides:

```yaml
api:
  resources:
    requests:
      memory: "512Mi"
      cpu: "500m"
    limits:
      memory: "1Gi"
      cpu: "1000m"
```

## Deployment Steps

### First Deployment (No Existing Database)

1. **Disable migrations** for initial deployment:
   ```yaml
   migrations:
     enabled: false
   ```

2. Deploy the application (creates infrastructure including PostgreSQL)

3. **Manually create database schema** or apply initial migrations

4. **Enable migrations** for future deployments:
   ```yaml
   migrations:
     enabled: true
   ```

5. Redeploy - migrations will run automatically before each deployment

### Subsequent Deployments

1. Update `global.imageTag` in gitops repository

2. ArgoCD will automatically sync and:
   - Run migrations job (PreSync hook)
   - Rolling update API and Embedding services

3. Monitor deployment:
   ```bash
   kubectl get pods -n rag-framework -w
   ```

## Verification

### Check Deployment Status

```bash
# All resources
kubectl get all -n rag-framework

# Pods
kubectl get pods -n rag-framework

# Services
kubectl get svc -n rag-framework

# Persistent Volume Claims
kubectl get pvc -n rag-framework
```

### Check Migration Job

```bash
# View migration job
kubectl get jobs -n rag-framework

# View migration logs
kubectl logs -n rag-framework job/<job-name>
```

### Health Checks

```bash
# Port-forward API service
kubectl port-forward -n rag-framework svc/<release-name>-ragcore-api 8080:80

# Check liveness
curl http://localhost:8080/healthz

# Check readiness
curl http://localhost:8080/healthz/ready

# Check detailed health
curl http://localhost:8080/api/admin/health
```

### View Logs

```bash
# API logs
kubectl logs -n rag-framework -l app.kubernetes.io/component=api -f

# Embedding service logs
kubectl logs -n rag-framework -l app.kubernetes.io/component=embedding -f

# All RAG services
kubectl logs -n rag-framework -l app.kubernetes.io/name=ragframework -f
```

## Troubleshooting

### Migration Job Fails

1. Check migration job logs:
   ```bash
   kubectl logs -n rag-framework job/<migration-job-name>
   ```

2. Common issues:
   - **Connection string incorrect**: Verify `postgres-connection` secret
   - **Database doesn't exist**: Create database manually first
   - **Permissions**: Ensure database user has DDL permissions

3. Re-run migrations:
   ```bash
   # Delete failed job
   kubectl delete job -n rag-framework <migration-job-name>

   # Re-deploy to trigger new migration job
   helm upgrade --install rag-framework ./helm ...
   ```

### Pods Not Starting

1. Check pod status:
   ```bash
   kubectl describe pod -n rag-framework <pod-name>
   ```

2. Common issues:
   - **ImagePullBackOff**: Check image pull secrets
   - **CrashLoopBackOff**: Check logs for application errors
   - **Pending**: Check resource availability

### Image Pull Errors

1. Verify credentials:
   ```bash
   kubectl get secret ghcr-credentials -n rag-framework -o jsonpath='{.data.\.dockerconfigjson}' | base64 -d
   ```

2. Test manual pull:
   ```bash
   docker login ghcr.io -u <username> -p <pat>
   docker pull ghcr.io/bardin08/ragframework/ragcore-api:v1.2.3-abc1234
   ```

### Service Not Accessible

1. Check service endpoints:
   ```bash
   kubectl get endpoints -n rag-framework
   ```

2. Check pod readiness:
   ```bash
   kubectl get pods -n rag-framework
   ```

3. Test internal connectivity:
   ```bash
   kubectl run -it --rm debug --image=curlimages/curl --restart=Never -n rag-framework -- \
     curl http://<service-name>:80/healthz
   ```

## Rollback

### ArgoCD Rollback

```bash
# Rollback to previous sync
argocd app rollback rag-framework
```

### Helm Rollback

```bash
# List releases
helm history rag-framework -n rag-framework

# Rollback to specific revision
helm rollback rag-framework <revision> -n rag-framework
```

## Scaling

### Manual Scaling

```bash
# Scale API
kubectl scale deployment -n rag-framework <release-name>-ragcore-api --replicas=5

# Scale Embedding service
kubectl scale deployment -n rag-framework <release-name>-ragcore-embedding --replicas=3
```

### Enable Horizontal Pod Autoscaler

Update values:
```yaml
autoscaling:
  api:
    enabled: true
    minReplicas: 2
    maxReplicas: 10
    targetCPUUtilizationPercentage: 70
```

## Monitoring

### Resource Usage

```bash
# Pod resource usage
kubectl top pods -n rag-framework

# Node resource usage
kubectl top nodes
```

### Events

```bash
# Watch events
kubectl get events -n rag-framework --sort-by='.lastTimestamp'
```

---

For more information:
- [Development Guide](./DEVELOPMENT.md)
- [Versioning Strategy](./VERSIONING.md)
- [Deployment Plan](./DEPLOYMENT-PLAN.md)
