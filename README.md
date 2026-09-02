# PIMS: Personal Inventory Maintenance System
*(Formerly known as WankPlanner)*

PIMS is a hyper-localized, offline-first telemetry and maintenance tracking application designed to monitor reproductive health cycles, biological baseline optimization, and supplement efficacy. It utilizes passive data entry to map recovery floors, turnover ceilings, and volume yields without relying on intrusive active monitoring.

## Tech Stack
* **Backend:** C# / .NET 8.0
* **Desktop Shell:** Photino.NET (Cross-platform native OS windowing)
* **Database:** SQLite (Local, air-gapped data persistence)
* **Frontend:** HTML5, CSS3, Vanilla JavaScript, Chart.js

## Core Features
* **Air-gapped & Local:** No cloud sync, no external servers. All data is written to a local `inventory.db` SQLite file.
* **Stealth Mode:** Hardware-level keybinding (Press `Escape`) instantly applies a CSS blur filter and disables pointer events on all sensitive data.
* **Auto-Calibration Engine:** Mathematically analyzes historical recovery gaps to automatically recommend personalized Floor (minimum rest) and Ceiling (max limit) thresholds.
* **Supplement Efficacy Analysis:** Runs statistical comparisons on contiguous datasets to prove whether Zinc increases volume yield and whether Maca Root accelerates recovery speed.
* **Sandbox Simulation:** Built-in test generator that simulates 6 months (150 records) of biologically weighted data to test the math engines safely.

## Build & Run Instructions

### Prerequisites
* .NET SDK 8.0+ installed on your system.

### Running the Application
To launch the desktop UI in development mode:
```bash
dotnet run

Headless Quick-Log

To log a standard "Maintenance" event instantly from the terminal or a bash script without opening the UI:
Bash

dotnet run -- --log

Publishing a Standalone Executable

To build a self-contained executable for Linux (Nobara/KDE):
Bash

dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true# Sexual Health & Frequency Tracker (dad-planner)

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
