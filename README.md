# PIMS: Personal Inventory Maintenance System

(Formerly known as dad-planner / WankPlanner)

<img width="2560" height="1440" alt="SMU-dad-planner" src="https://github.com/user-attachments/assets/c8f53f3d-7bd1-42bf-8d2c-5f5a956c1998" />


Standard calendar apps aren't built for clinical reproductive health. When you need to manage strict medical testing requirements (like OHSU semen analysis protocols), maintain baseline prostate health with rigid turnover limits, and track supplement efficacy, you need precise telemetry. More importantly, you need that data kept completely offline.

PIMS is a highly over-engineered, offline-first sexual frequency, biological baseline, and clinical tracker. It enforces routine health cycles, captures specific clinical variables (thermal stress, subjective volume, biological saturation), and provides interactive local statistical analysis—all while ensuring your most private biological data never leaves your machine.

## 🏗️ Architecture Stack

* **Backend:** C# / .NET 8.0 (Headless background capabilities, native OS notifications, QuestPDF for native vector document generation)
* **Frontend:** Photino.NET (Native cross-platform webview: WebView2 on Windows, WebKitGTK on Linux, WKWebView on macOS)
* **UI/UX:** HTML5, CSS3, Vanilla JavaScript
* **Database:** SQLite3 (Local Only, WAL-mode enabled, connection pooling disabled for aggressive OS un-locking, BLOB storage for raw files)
* **CI/CD:** Automated GitHub Actions pipeline for multi-OS binary compilation

## 📦 Dependencies & Libraries

**Backend (C# / NuGet):**

* **Photino.NET** - Cross-platform native window and webview bindings.
* **Microsoft.Data.Sqlite** - Lightweight, local database driver for WAL-mode telemetry storage.
* **QuestPDF** - Native C# vector graphics engine for synthesizing the 90-Day PDF reports.

**Frontend:**

* **ApexCharts** - Lightweight SVG charting library for the interactive timeline, event distribution, and yield profiles.

## ✨ Key Features

### 📊 Clinical Telemetry & Tracking

* **Dynamic Telemetry HUD:** Real-time calculation of time elapsed since your last event, rolling averages, and maximum endurance gaps.
* **Configurable Boundary Thresholds:** Set your own clinical "Floor" (minimum refractory period to avoid volume depletion) and "Ceiling" (maximum hours between routine maintenance). Breaching these triggers native OS desktop notifications.
* **Quad-State Logging:** Distinguish between Maintenance (solo/blue), Playtime (recreational/red), Baby-Making (conception/green), and Clinical-Lab (medical baselines/slate) with single-click action buttons.
* **Granular Clinical Metrics:** Dedicated numerical inputs for formal semen analysis parameters, capturing Clinical Volume (mL), Concentration (M), Total Motility (%), Progressive Motility (%), Morphology (%), and pH Level.
* **Lab Report PDF Vault:** Attach, store (as SQLite BLOBs), and launch original laboratory PDF results directly from the dashboard via your native OS document viewer.
* **Pre-Log Modifiers:** Track crucial biological variables like subjective volume (Dry/Low/Normal/High), a 4-level Thermal Stress Index, and dietary supplement stacks (Zinc, Maca).
* **365-Day Activity Matrix:** GitHub-style density heatmap plotting year-round event frequency and maximum daily volume yields.
* **Interactive Charting:** Pan/zoom-enabled timeline generated via ApexCharts, featuring custom tooltips that display the clinical metadata, lab metrics, and biological saturation flags for every recorded gap.

### 🏥 Medical Workflows & Analytics

* **74-Day Thermal Shadow Engine:** Maps the delayed biological impact of severe heat events (>101°F fever or prolonged hot tub exposure) on spermatogenesis. Automatically flags the system as compromised for a full 74-day cycle to prevent corrupting statistical baselines or wasting money on premature clinical testing.
* **Ground-Truth Clinical Override:** Dynamically breaks an active Thermal Shadow if a subsequent formal lab test returns normal WHO baseline metrics (≥ 15M/mL Concentration, ≥ 40% Motility), proving system health and restoring analytical tracking.
* **Biological Saturation Analysis:** Evaluates physiological buildup by mapping a 21-day lagging window to determine supplement saturation. Runs statistical comparisons on contiguous, uncompromised datasets (excluding Thermal Shadows) to prove whether Zinc mathematically increases volume yield and whether Maca Root accelerates recovery speed.
* **Auto-Calibration Engine:** Mathematically analyzes historical recovery gaps to automatically recommend personalized Floor and Ceiling thresholds based on your standard deviation.
* **90-Day Retrospective Report:** Instantly synthesize your last three months of data into a formatted, printable PDF. Utilizes native C# vector drawing to generate crisp Event Distribution and Yield Profile bar charts.
* **Clinical Blackout Mode:** Locks in mandatory abstinence windows and suppresses all overdue notifications prior to scheduled medical baseline testing (e.g., OHSU Andrology Lab protocols).
* **Inline Data Correction:** Retroactively fix timestamps, event modes, volume metrics, or update attached lab PDFs via the scrollable log table.

### 🔒 Security & Privacy

* **Fully Air-Gapped:** Zero external API calls, no cloud sync. All data is written exclusively to a local `inventory.db` file.
* **Stealth Mode (Panic Button):** Hardware-level keybinding (Press `Escape`) instantly applies a CSS blur filter and disables pointer events on all sensitive data on the screen.
* **Non-Destructive Sandbox Mode:** Safely swaps your live SQLite database into a backup partition, seeds the UI with 150 records of procedurally generated, biologically weighted fake data for visual testing, and seamlessly restores your real data when you exit.

## 🛠️ Installation & Setup

**Prerequisites**

* .NET SDK 8.0+ installed on your system.
* For Linux users: `webkit2gtk4.0` is required for the Photino UI.

```bash
# Nobara/Fedora
sudo dnf install webkit2gtk4.0

```

**Running the Application in Dev Mode**
To launch the desktop UI directly from the source code:

```bash
dotnet run

```

**Headless Quick-Log**
To log a standard "Maintenance" event instantly from the terminal or a bash script without opening the UI:

```bash
dotnet run -- --log

```

## 🚀 Compilation & Deployment

PIMS is configured to compile into standalone executables via standard .NET publish commands.

**⚠️ OS Stability Warning**

* **Linux (Nobara/KDE):** Fully tested, stable, and production-ready. Wayland `.Center()` window quirks resolved.
* **Windows:** Currently experiencing known issues with aggressive OS-level file locking and SQLite connection pooling that prevents reliable database writes. Proceed with caution.
* **macOS:** Compiled successfully but completely untested on native hardware.

**Compile for Linux (Tested & Stable):**

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

```

**Compile for Windows (Experimental / Unstable):**
*(Note: Windows builds attempt to utilize aggressive SQLite permission overrides and WAL-mode to prevent silent OS-level connection lockouts, but issues persist).*

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

```

**Compile for macOS (Untested):**

```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

```
