#!/bin/bash

# Define paths
SERVICE_DIR="$HOME/.config/systemd/user"
SERVICE_FILE="$SERVICE_DIR/wankplanner.service"
APP_DIR="/home/brian/wankplanner"
EXEC_PATH="$APP_DIR/bin/Debug/net10.0/wankplanner"

echo "[+] Creating systemd user directory..."
mkdir -p "$SERVICE_DIR"

echo "[+] Writing service unit file..."
cat <<EOF > "$SERVICE_FILE"
[Unit]
Description=WankPlanner Personal Maintenance Alert Daemon
After=graphical-session.target

[Service]
Type=simple
WorkingDirectory=$APP_DIR
ExecStart=$EXEC_PATH --daemon
Restart=on-failure
RestartSec=10
Environment=DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$UID/bus

[Install]
WantedBy=default.target
EOF

echo "[+] Reloading systemd user daemon..."
systemctl --user daemon-reload

echo "[+] Enabling and starting WankPlanner daemon..."
systemctl --user enable --now wankplanner.service

echo "[+] Setup complete. Current status:"
systemctl --user status wankplanner.service --no-pager
