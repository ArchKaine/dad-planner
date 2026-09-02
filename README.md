# Sexual Health & Frequency Tracker (dad-planner)

Standard calendar apps aren't built for clinical reproductive health. When you need to manage strict medical testing requirements (like OHSU semen analysis protocols), maintain baseline prostate health with rigid turnover limits, and track fertility efforts, you need precise telemetry. More importantly, you need that data kept completely off the cloud.

This is a highly over-engineered, offline-first sexual frequency and clinical tracker built natively for Linux/Wayland. It enforces routine health cycles, captures specific clinical variables (fever flags, subjective volume, event types), and provides interactive local statistical analysis—all while ensuring your most private biological data never leaves your machine.

## 🏗️ Architecture Stack
* **Backend:** C# / .NET 10.0 (Headless Background Daemon)
* **Frontend:** Photino.NET (Native WebKitGTK Webview, dynamically autosizing)
* **System Tray:** Python / PyQt6 (Wayland-Native KDE Plasma integration)
* **Database:** SQLite3 (Local Only)
* **Analytics:** Air-gapped Chart.js
* **Disaster Recovery:** `systemd` timers

## ✨ Key Features

### 📊 Clinical Telemetry & Tracking
* **Dynamic Telemetry HUD:** Real-time calculation of time elapsed since your last event, rolling averages, and maximum endurance gaps.
* **Configurable Boundary Thresholds:** Set your own clinical "Floor" (minimum refractory period to avoid volume depletion) and "Ceiling" (maximum hours between routine maintenance). Breaching these triggers KDE desktop notifications.
* **Tri-State Logging:** Distinguish between *Maintenance* (solo), *Playtime* (recreational), and *Baby-Making* (conception) with single-click action buttons. 
* **Pre-Log Modifiers:** Track crucial biological variables like subjective volume (Low/Normal/High) and heat/fever exposure (which impacts spermatogenesis). The system automatically locks out volume tracking for partnered events.
* **Interactive Charting:** Color-coded line graphs based on event type, featuring hover tooltips that display the clinical metadata for every recorded gap.

### 🏥 Medical Workflows
* **Clinical Blackout Mode:** Locks in mandatory abstinence windows and suppresses all overdue notifications prior to scheduled medical baseline testing (e.g., OHSU semen analysis protocols).
* **Inline Data Correction:** Made a mistake? Click 'EDIT' on any row in the scrollable log table to retroactively fix timestamps, event modes, or volume metrics on the fly.
* **Non-Destructive Sandbox Mode:** Safely swaps your live SQLite database into a backup partition, seeds the UI with procedurally generated fake data for visual testing, and seamlessly restores your real data when you exit.

### 🔒 Security & Privacy
* **Fully Air-Gapped:** Zero external API calls. Chart.js is bundled locally.
* **Automated Data Protection:** Integrated `systemd` timers for daily `.gz` database snapshots with a 365-day rolling retention policy.
* **Stealth Mode (Panic Button):** Hardware hotkey (`Esc`) immediately blurs all telemetry data and obfuscates the UI. (A vestigial sysadmin feature, but it's there).

---

## 🛠️ Installation & Setup

### 1. Prerequisites
Ensure you have the following installed on your Nobara/Fedora system:
```bash
sudo dnf install dotnet-sdk-10.0 python3 python3-pip webkit2gtk4.0

```

### 2. Python Virtual Environment (System Tray)

The system tray utilizes PyQt6. To avoid system package conflicts, initialize a local virtual environment:

```bash
# Navigate to the project directory
cd dad-planner

# Create and activate the virtual environment
python -m venv venv
source venv/bin/activate

# Install the required Qt libraries
pip install PyQt6

```

### 3. Build the C# Backend

Compile the Photino.NET/C# application.

```bash
dotnet build

```

### 4. Deploy Automated Backups

Initialize the SQLite database backup system. This sets up a background `systemd` timer to safely snapshot and compress your history every night at midnight.

```bash
chmod +x install_backup.sh
./install_backup.sh

```

---

## 🚀 Execution

### Running the Application (Wayland Note)

Wayland compositors occasionally cause memory instability (`free(): corrupted unsorted chunks`) with WebKitGTK. To bypass this, launch the application with compositing mode disabled.

```bash
# 1. Activate the Python environment
source venv/bin/activate

# 2. Launch with Wayland hardware acceleration explicitly disabled
WEBKIT_DISABLE_COMPOSITING_MODE=1 python tray_app.py

```
