#!/bin/bash

# Bank Management System Deployment Script
set -e

# Configuration
ENVIRONMENT=${1:-staging}
NAMESPACE="bank-app"
DOCKER_REGISTRY="ghcr.io"
IMAGE_TAG=${2:-latest}

echo "🚀 Starting deployment to $ENVIRONMENT environment..."

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl is not installed or not in PATH"
    exit 1
fi

# Check if we're connected to the right cluster
echo "📋 Current Kubernetes context:"
kubectl config current-context

read -p "Is this the correct cluster for $ENVIRONMENT? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "❌ Deployment cancelled"
    exit 1
fi

# Create namespace if it doesn't exist
echo "🔧 Creating namespace if it doesn't exist..."
kubectl apply -f devops/kubernetes/namespace.yaml

# Apply ConfigMaps and Secrets
echo "🔐 Applying configuration..."
kubectl apply -f devops/kubernetes/configmap.yaml

# Deploy database
echo "🗄️ Deploying database..."
kubectl apply -f devops/kubernetes/database.yaml

# Wait for database to be ready
echo "⏳ Waiting for database to be ready..."
kubectl wait --for=condition=ready pod -l app=database -n $NAMESPACE --timeout=300s

# Deploy backend
echo "🔧 Deploying backend..."
kubectl set image deployment/backend backend=$DOCKER_REGISTRY/bank-management-system/backend:$IMAGE_TAG -n $NAMESPACE || \
kubectl apply -f devops/kubernetes/backend.yaml

# Wait for backend rollout
echo "⏳ Waiting for backend deployment..."
kubectl rollout status deployment/backend -n $NAMESPACE --timeout=300s

# Deploy frontend
echo "🎨 Deploying frontend..."
kubectl set image deployment/frontend frontend=$DOCKER_REGISTRY/bank-management-system/frontend:$IMAGE_TAG -n $NAMESPACE || \
kubectl apply -f devops/kubernetes/frontend.yaml

# Wait for frontend rollout
echo "⏳ Waiting for frontend deployment..."
kubectl rollout status deployment/frontend -n $NAMESPACE --timeout=300s

# Apply ingress
echo "🌐 Configuring ingress..."
kubectl apply -f devops/kubernetes/ingress.yaml

# Health check
echo "🏥 Performing health checks..."
sleep 30

# Check if all pods are running
echo "📊 Checking pod status..."
kubectl get pods -n $NAMESPACE

# Check services
echo "🔗 Checking services..."
kubectl get services -n $NAMESPACE

# Check ingress
echo "🌍 Checking ingress..."
kubectl get ingress -n $NAMESPACE

echo "✅ Deployment to $ENVIRONMENT completed successfully!"
echo "🔗 Application should be available at the configured ingress URL"

# Optional: Run smoke tests
if [[ "$ENVIRONMENT" == "staging" ]]; then
    echo "🧪 Running smoke tests..."
    ./devops/scripts/smoke-tests.sh
fi