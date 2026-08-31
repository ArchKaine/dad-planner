import sys
import os
import time
import sqlite3
import subprocess
from PyQt6.QtWidgets import QApplication, QSystemTrayIcon, QMenu
from PyQt6.QtGui import QIcon, QPixmap, QPainter, QColor

DB_PATH = os.path.expanduser('~/wankplanner/inventory.db')
APP_EXEC = os.path.expanduser('~/wankplanner/bin/Debug/net10.0/wankplanner')

def log_event():
    """Writes directly to SQLite and triggers the KDE notification."""
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("INSERT INTO maintenance_log (timestamp) VALUES (?)", (int(time.time()),))
    conn.commit()
    conn.close()
    
    subprocess.run([
        'notify-send',
        '-u', 'low',
        '-a', 'System Monitor',
        'System Task',
        'Protocol logged successfully.'
    ])

def open_dashboard():
    """Launches the C# UI with Wayland/GTK safety flags."""
    env = os.environ.copy()
    # Suppress WebKitGTK hardware acceleration memory bugs on Wayland
    env["WEBKIT_DISABLE_COMPOSITING_MODE"] = "1"
    
    subprocess.Popen(
        [APP_EXEC],
        env=env,
        stdout=subprocess.DEVNULL,  # Send standard output to the void
        stderr=subprocess.DEVNULL   # Send crash logs/GTK warnings to the void
    )

def create_icon():
    """Draws a native Qt pixmap of a blue circle."""
    pixmap = QPixmap(64, 64)
    pixmap.fill(QColor("transparent"))
    
    painter = QPainter(pixmap)
    painter.setBrush(QColor(0, 122, 204))
    painter.setPen(QColor("transparent"))
    painter.drawEllipse(16, 16, 32, 32)
    painter.end()
    
    return QIcon(pixmap)

# Initialize the Qt Application
app = QApplication(sys.argv)
# Prevent the app from closing if no standard windows are open
app.setQuitOnLastWindowClosed(False)

# Build the System Tray Icon
tray = QSystemTrayIcon(create_icon(), app)
tray.setToolTip("System Monitor")

# Build the Context Menu
menu = QMenu()

log_action = menu.addAction("Execute Maintenance Protocol")
log_action.triggered.connect(log_event)

open_action = menu.addAction("Open Dashboard")
open_action.triggered.connect(open_dashboard)

menu.addSeparator()

exit_action = menu.addAction("Exit Service")
exit_action.triggered.connect(app.quit)

tray.setContextMenu(menu)
tray.show()

# Run the event loop
sys.exit(app.exec())
