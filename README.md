# Sexual Health & Frequency Tracker (dad-planner)

A highly over-engineered, offline-first orgasm and sexual frequency tracker built natively for Linux/Wayland. 

Designed to track personal habits, enforce maximum time limits between sessions, and provide local statistical analysis without sending highly private biological data to the cloud.

## 🏗️ Architecture Stack
* **Backend:** C# / .NET 10.0 (Headless Background Daemon)
* **Frontend:** Photino.NET (Native WebKitGTK Webview)
* **System Tray:** Python / PyQt6 (Wayland-Native KDE Plasma integration)
* **Database:** SQLite3
* **Analytics:** Air-gapped Chart.js
* **Disaster Recovery:** `systemd` timers

## ✨ Key Features
* **Telemetry HUD:** Real-time calculation of the time elapsed since your last climax, rolling averages, and maximum endurance gaps.
* **The 72-Hour Limit:** Visual countdowns to ensure you don't go more than 3 days without a release, complete with desktop warnings.
* **Clinical Blackout Mode:** Tracks mandatory abstinence windows and suppresses notifications prior to scheduled medical testing (e.g., OHSU semen analysis).
* **Automated Data Protection:** Integrated `systemd` timers for daily `.gz` database snapshots with a 365-day rolling retention policy.
* **Stealth Mode (Panic Button):** Hardware hotkey (`Esc`) immediately blurs all telemetry data and obfuscates the UI if privacy is suddenly required.

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

Compile the Photino/C# application.

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

*Note: Once running, the application lives entirely in the KDE system tray. Left-click the icon to open the telemetry dashboard. Right-click to access quick-log and exit options.*

---

## 🔒 Security & Privacy

* **Fully Air-Gapped:** Zero external API calls. Chart.js is bundled locally.
* **Data Retention:** SQLite database (`inventory.db`) is entirely local.

```

This broadens the scope to cover both solo and partnered activity, drops the abstraction entirely, and keeps the formatting perfectly clean for GitHub's markdown renderer.

```# PIMS (Personal Inventory & Maintenance System)

An offline-first, enterprise-grade biological telemetry and logistics dashboard built natively for Linux/Wayland. 

Designed to track routine systemic turnover, enforce SLA blackout windows, and provide local statistical analysis without relying on external CDNs or cloud architecture.

## 🏗️ Architecture Stack
* **Backend:** C# / .NET 10.0 (Headless Background Daemon)
* **Frontend:** Photino.NET (Native WebKitGTK Webview)
* **System Tray:** Python / PyQt6 (Wayland-Native KDE Plasma integration)
* **Database:** SQLite3
* **Analytics:** Air-gapped Chart.js
* **Disaster Recovery:** `systemd` timers

## ✨ Key Features
* **Telemetry HUD:** Real-time calculation of current delta, rolling averages, and maximum endurance.
* **SLA Timers:** Visual countdowns to mandatory 72-hour maintenance limits.
* **Clinical Blackout Mode:** Suppresses routine notifications prior to scheduled medical baseline testing (OHSU target).
* **Automated Disaster Recovery:** Integrated `systemd` timers for daily `.gz` database snapshots with a 365-day rolling retention policy.
* **Stealth Mode:** Hardware hotkey (`Esc`) UI obfuscation.

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

Compile the Photino/C# application.

```bash
dotnet build

```

### 4. Deploy Automated Backups

Initialize the SQLite database backup system. This sets up a background `systemd` timer to compress and archive the database every night at midnight with a 365-day retention policy.

```bash
chmod +x install_backup.sh
./install_backup.sh

```

---

## 🚀 Execution

### Running the Application (Wayland Note)

Wayland compositors occasionally cause memory instability (`free(): corrupted unsorted chunks`) with WebKitGTK. To bypass this, the application must be launched with the compositing mode disabled.

You can launch the full suite by activating the virtual environment and running the tray script. The tray will handle launching the backend daemon.

```bash
# 1. Activate the Python environment
source venv/bin/activate

# 2. Launch with Wayland hardware acceleration explicitly disabled
WEBKIT_DISABLE_COMPOSITING_MODE=1 python tray_app.py

```

*Note: Once running, the application lives entirely in the KDE system tray. Left-click the icon to open the telemetry dashboard. Right-click to access quick-log and exit options.*

---

## 🔒 Security & Privacy

* **Fully Air-Gapped:** Zero external API calls. Chart.js is bundled locally.
* **Data Retention:** SQLite database (`inventory.db`) is entirely local.
* **Panic Button:** Pressing `Escape` while the dashboard is open immediately blurs all telemetry data and masks the application interface.
