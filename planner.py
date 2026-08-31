import sqlite3
import time
import subprocess
import argparse
from datetime import datetime

DB_PATH = 'inventory.db'

def init_db():
    """Bootstraps the local SQLite database."""
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute('''
        CREATE TABLE IF NOT EXISTS maintenance_log (
            event_id INTEGER PRIMARY KEY AUTOINCREMENT,
            timestamp INTEGER,
            event_type TEXT
        )
    ''')
    conn.commit()
    conn.close()

def log_event(event_type="Routine Maintenance"):
    """Logs a new turnover event."""
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("INSERT INTO maintenance_log (timestamp, event_type) VALUES (?, ?)", 
              (int(time.time()), event_type))
    conn.commit()
    conn.close()
    
    current_time = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    print(f"[+] Logged: {event_type} at {current_time}")

def check_status():
    """Calculates time since last event and triggers KDE notification if overdue."""
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute("SELECT timestamp FROM maintenance_log ORDER BY timestamp DESC LIMIT 1")
    row = c.fetchone()
    conn.close()

    if row:
        last_event = row[0]
        delta_hours = (time.time() - last_event) / 3600
        
        if delta_hours > 72:
            # Pushes a discrete notification to the KDE Plasma desktop
            subprocess.run([
                'notify-send', 
                '-u', 'normal', 
                '-a', 'System Monitor', 
                'System Task', 
                'Routine Personal Maintenance Required.'
            ])
            print(f"[!] Alert triggered. Delta: {delta_hours:.1f} hours.")
        else:
            print(f"[✓] System optimal. Delta: {delta_hours:.1f} hours.")
    else:
        print("[-] No records found. Initial baseline required.")

if __name__ == "__main__":
    init_db()
    
    parser = argparse.ArgumentParser(description="PIMS: Personal Inventory Management System")
    parser.add_argument('--log', action='store_true', help='Log a new routine maintenance event')
    parser.add_argument('--check', action='store_true', help='Check status and trigger alerts if overdue')
    
    args = parser.parse_args()
    
    if args.log:
        log_event()
    elif args.check:
        check_status()
    else:
        parser.print_help()
