#!/bin/bash

APP_DIR="/home/brian/wankplanner"
BACKUP_DIR="$APP_DIR/backups"
SCRIPT_FILE="$APP_DIR/backup_db.sh"
SERVICE_DIR="$HOME/.config/systemd/user"

echo "[+] Creating backup directory and script..."
mkdir -p "$BACKUP_DIR"

# 1. Write the backup executable
cat << 'EOF' > "$SCRIPT_FILE"
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
EOF

chmod +x "$SCRIPT_FILE"

echo "[+] Writing systemd service and timer..."

# 2. Write the service unit (runs the script once)
cat <<EOF > "$SERVICE_DIR/wankplanner-backup.service"
[Unit]
Description=WankPlanner Database Backup Task

[Service]
Type=oneshot
ExecStart=$SCRIPT_FILE
EOF

# 3. Write the timer unit (triggers the service daily)
cat <<EOF > "$SERVICE_DIR/wankplanner-backup.timer"
[Unit]
Description=WankPlanner Daily Database Backup Timer

[Timer]
OnCalendar=daily
Persistent=true

[Install]
WantedBy=timers.target
EOF

echo "[+] Reloading systemd user daemon..."
systemctl --user daemon-reload

echo "[+] Enabling and starting backup timer..."
systemctl --user enable --now wankplanner-backup.timer

echo "[+] Setup complete. Triggering first backup manually to verify..."
systemctl --user start wankplanner-backup.service

echo "[+] Current backups in $BACKUP_DIR:"
ls -lh "$BACKUP_DIR"
