# PIMS: Personal Inventory Maintenance System
*(Formerly known as dad-planner / WankPlanner)*

Standard calendar apps aren't built for clinical reproductive health. When you need to manage strict medical testing requirements (like OHSU semen analysis protocols), maintain baseline prostate health with rigid turnover limits, and track supplement efficacy, you need precise telemetry. More importantly, you need that data kept completely offline.

PIMS is a highly over-engineered, offline-first sexual frequency, biological baseline, and clinical tracker. It enforces routine health cycles, captures specific clinical variables (fever flags, subjective volume, supplement stacks), and provides interactive local statistical analysis—all while ensuring your most private biological data never leaves your machine.

## 🏗️ Architecture Stack
* **Backend:** C# / .NET 8.0 (Headless background capabilities with native OS notifications)
* **Frontend:** Photino.NET (Native cross-platform webview: WebView2 on Windows, WebKitGTK on Linux, WKWebView on macOS)
* **UI/UX:** HTML5, CSS3, Vanilla JavaScript, ApexCharts (bundled)
* **Database:** SQLite3 (Local Only, WAL-mode enabled, connection pooling disabled for aggressive OS un-locking)
* **CI/CD:** Automated GitHub Actions pipeline for multi-OS binary compilation

## ✨ Key Features

### 📊 Clinical Telemetry & Tracking
* **Dynamic Telemetry HUD:** Real-time calculation of time elapsed since your last event, rolling averages, and maximum endurance gaps.
* **Configurable Boundary Thresholds:** Set your own clinical "Floor" (minimum refractory period to avoid volume depletion) and "Ceiling" (maximum hours between routine maintenance). Breaching these triggers native OS desktop notifications.
* **Tri-State Logging:** Distinguish between *Maintenance* (solo), *Playtime* (recreational), and *Baby-Making* (conception) with single-click action buttons. 
* **Pre-Log Modifiers:** Track crucial biological variables like subjective volume (Low/Normal/High), heat/fever exposure (which impacts spermatogenesis), and supplement stacks (Zinc, Maca).
* **Interactive Charting:** Interactive, pan/zoom-enabled timeline generated via ApexCharts, featuring custom tooltips that display the clinical metadata and supplement stacks for every recorded gap.

### 🏥 Medical Workflows & Analytics
* **Clinical Blackout Mode:** Locks in mandatory abstinence windows and suppresses all overdue notifications prior to scheduled medical baseline testing (e.g., OHSU Andrology Lab protocols).
* **Auto-Calibration Engine:** Mathematically analyzes historical recovery gaps to automatically recommend personalized Floor and Ceiling thresholds based on your standard deviation.
* **Supplement Efficacy Analysis:** Runs statistical comparisons on contiguous datasets to prove whether Zinc mathematically increases volume yield and whether Maca Root accelerates recovery speed.
* **Inline Data Correction:** Retroactively fix timestamps, event modes, or volume metrics via the scrollable log table.

### 🔒 Security & Privacy
* **Fully Air-Gapped:** Zero external API calls, no cloud sync. All data is written to a local `inventory.db` file.
* **Stealth Mode (Panic Button):** Hardware-level keybinding (Press `Escape`) instantly applies a CSS blur filter and disables pointer events on all sensitive data.
* **Non-Destructive Sandbox Mode:** Safely swaps your live SQLite database into a backup partition, seeds the UI with 150 records of procedurally generated, biologically weighted fake data for visual testing, and seamlessly restores your real data when you exit.

---

## 🛠️ Installation & Setup

### Prerequisites
* .NET SDK 8.0+ installed on your system.
* For Linux users: `webkit2gtk4.0` is required for the Photino UI.
  ```bash
  # Nobara/Fedora
  sudo dnf install webkit2gtk4.0

```

### Running the Application in Dev Mode

To launch the desktop UI directly from the source code:

```bash
dotnet run

```

### Headless Quick-Log

To log a standard "Maintenance" event instantly from the terminal or a bash script without opening the UI:

```bash
dotnet run -- --log

```

## 🚀 Compilation & Deployment

PIMS is configured to compile into standalone executables via standard .NET publish commands.

> ⚠️ **OS Stability Warning**
> * **Linux (Nobara/KDE):** Fully tested, stable, and production-ready.
> * **Windows:** Currently experiencing known issues with aggressive OS-level file locking and SQLite connection pooling that prevents reliable database writes. Proceed with caution.
> * **macOS:** Compiled successfully but completely untested on native hardware.
> 
> 

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

```

```
