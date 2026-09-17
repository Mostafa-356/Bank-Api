#!/bin/bash

# Database Backup Script for Bank Management System
set -e

# Configuration
BACKUP_DIR="/backups"
DATE=$(date +%Y%m%d_%H%M%S)
NAMESPACE="bank-app"
DB_POD=$(kubectl get pods -n $NAMESPACE -l app=database -o jsonpath='{.items[0].metadata.name}')
RETENTION_DAYS=30

echo "🗄️ Starting database backup..."

# Create backup directory if it doesn't exist
mkdir -p $BACKUP_DIR

# Perform database backup
echo "📦 Creating backup of BankDB..."
kubectl exec -n $NAMESPACE $DB_POD -- /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" \
    -Q "BACKUP DATABASE [BankDB] TO DISK = N'/tmp/bankdb_backup_$DATE.bak' WITH NOFORMAT, NOINIT, NAME = 'BankDB-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10"

# Copy backup file from pod to local storage
echo "📥 Copying backup file..."
kubectl cp $NAMESPACE/$DB_POD:/tmp/bankdb_backup_$DATE.bak $BACKUP_DIR/bankdb_backup_$DATE.bak

# Compress backup
echo "🗜️ Compressing backup..."
gzip $BACKUP_DIR/bankdb_backup_$DATE.bak

# Clean up old backups
echo "🧹 Cleaning up old backups (older than $RETENTION_DAYS days)..."
find $BACKUP_DIR -name "bankdb_backup_*.bak.gz" -mtime +$RETENTION_DAYS -delete

# Clean up temporary file in pod
kubectl exec -n $NAMESPACE $DB_POD -- rm -f /tmp/bankdb_backup_$DATE.bak

echo "✅ Backup completed: $BACKUP_DIR/bankdb_backup_$DATE.bak.gz"

# Optional: Upload to cloud storage
if [[ -n "$AWS_S3_BUCKET" ]]; then
    echo "☁️ Uploading to S3..."
    aws s3 cp $BACKUP_DIR/bankdb_backup_$DATE.bak.gz s3://$AWS_S3_BUCKET/backups/
fi

if [[ -n "$AZURE_STORAGE_ACCOUNT" ]]; then
    echo "☁️ Uploading to Azure Storage..."
    az storage blob upload \
        --account-name $AZURE_STORAGE_ACCOUNT \
        --container-name backups \
        --name bankdb_backup_$DATE.bak.gz \
        --file $BACKUP_DIR/bankdb_backup_$DATE.bak.gz
fi