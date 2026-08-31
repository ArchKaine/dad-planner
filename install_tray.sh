#!/bin/bash

AUTOSTART_DIR="$HOME/.config/autostart"
DESKTOP_FILE="$AUTOSTART_DIR/wankplanner-tray.desktop"
VENV_PYTHON="/home/brian/wankplanner/venv/bin/python"
TRAY_SCRIPT="/home/brian/wankplanner/tray.py"

echo "[+] Creating KDE autostart directory..."
mkdir -p "$AUTOSTART_DIR"

echo "[+] Writing desktop entry..."
cat <<EOF > "$DESKTOP_FILE"
[Desktop Entry]
Type=Application
Name=System Monitor
Comment=System Maintenance Tray Service
Exec=$VENV_PYTHON $TRAY_SCRIPT
Terminal=false
StartupNotify=false
NoDisplay=true
EOF

chmod +x "$DESKTOP_FILE"

echo "[+] Setup complete. The tray icon will now launch silently on login."
