#!/bin/bash
APP_DIR="/home/brian/wankplanner"
BACKUP_DIR="$APP_DIR/backups"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="$BACKUP_DIR/inventory_$TIMESTAMP.db"

# Safely snapshot the live database
sqlite3 "$APP_DIR/inventory.db" ".backup '$BACKUP_FILE'"

# Compress the snapshot to save space
gzip "$BACKUP_FILE"

# Prune backups older than 365 days (1 year)
find "$BACKUP_DIR" -name "inventory_*.db.gz" -type f -mtime +365 -delete
