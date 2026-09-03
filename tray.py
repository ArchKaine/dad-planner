import sys
import os
import time
import sqlite3
import subprocess
import platform
from pathlib import Path
from PyQt6.QtWidgets import QApplication, QSystemTrayIcon, QMenu
from PyQt6.QtGui import QIcon, QPixmap, QPainter, QColor

OS_TYPE = platform.system()

# Dynamically route DB path to match the C# logic
if OS_TYPE == "Windows":
    DB_DIR = Path(os.getenv('LOCALAPPDATA', '')) / "PIMS"
    APP_EXEC_NAME = "wankplanner.exe"
else:
    # Standard for Linux (including KDE Plasma/Nobara) and macOS
    DB_DIR = Path.home() / ".local" / "share" / "PIMS"
    APP_EXEC_NAME = "wankplanner"

DB_PATH = DB_DIR / "inventory.db"

# Resolve executable path dynamically, checking both Release and Debug folders
APP_DIR = Path(__file__).parent.resolve()
APP_EXEC = APP_DIR / "bin" / "Release" / "net10.0" / APP_EXEC_NAME
if not APP_EXEC.exists():
    APP_EXEC = APP_DIR / "bin" / "Debug" / "net10.0" / APP_EXEC_NAME


def show_notification(title, message):
    """Cross-platform system notifications."""
    try:
        if OS_TYPE == "Windows":
            # Uses PowerShell to spawn a native Windows balloon tip
            ps_cmd = f"Add-Type -AssemblyName System.Windows.Forms; $n = New-Object System.Windows.Forms.NotifyIcon; $n.Icon = [System.Drawing.SystemIcons]::Information; $n.Visible = $true; $n.ShowBalloonTip(5000, '{title}', '{message}', 'Info'); Start-Sleep -Seconds 5; $n.Dispose()"
            subprocess.run(
                ["powershell", "-WindowStyle", "Hidden", "-Command", ps_cmd],
                creationflags=subprocess.CREATE_NO_WINDOW
            )
        elif OS_TYPE == "Darwin":
            subprocess.run(['osascript', '-e', f'display notification "{message}" with title "{title}"'])
        else:
            # Native libnotify for Linux/KDE
            subprocess.run(['notify-send', '-u', 'low', '-a', 'System Monitor', title, message])
    except Exception:
        pass  # Failsafe if the notification daemon is missing


def log_event():
    """Writes directly to SQLite and triggers the notification."""
    DB_DIR.mkdir(parents=True, exist_ok=True)
    
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    
    # Ensured table name matches the C# initialization schema ('Logs')
    c.execute("CREATE TABLE IF NOT EXISTS Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER)")
    c.execute("INSERT INTO Logs (Timestamp) VALUES (?)", (int(time.time()),))
    
    conn.commit()
    conn.close()
    
    show_notification('System Task', 'Protocol logged successfully.')


def open_dashboard():
    """Launches the C# UI with OS-specific safety flags."""
    if not APP_EXEC.exists():
        show_notification("Error", f"Could not find executable at {APP_EXEC}")
        return

    env = os.environ.copy()
    kwargs = {}
    
    if OS_TYPE == "Linux":
        # Suppress WebKitGTK hardware acceleration memory bugs on Wayland
        env["WEBKIT_DISABLE_COMPOSITING_MODE"] = "1"
    elif OS_TYPE == "Windows":
        # Suppress the ghost console window popup on Windows
        kwargs['creationflags'] = subprocess.CREATE_NO_WINDOW
    
    subprocess.Popen(
        [str(APP_EXEC)],
        env=env,
        stdout=subprocess.DEVNULL,  # Send standard output to the void
        stderr=subprocess.DEVNULL,  # Send crash logs/GTK warnings to the void
        **kwargs
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


if __name__ == "__main__":
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
