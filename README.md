cat << 'EOF' > README.md
# PIMS (Personal Inventory & Maintenance System)

An offline-first, enterprise-grade biological telemetry and logistics dashboard built for Linux/Wayland. 

Designed to track routine systemic turnover, enforce SLA blackout windows, and provide local statistical analysis without relying on external CDNs or cloud architecture.

## Architecture
* **Backend:** C# / .NET 10.0 (Headless Background Daemon)
* **Frontend:** Photino.NET (Native WebKitGTK Webview)
* **System Tray:** Python / PyQt6 (Wayland-Native KDE Plasma integration)
* **Database:** SQLite3
* **Analytics:** Air-gapped Chart.js

## Features
* **Telemetry HUD:** Real-time calculation of current delta, rolling averages, and maximum endurance.
* **SLA Timers:** Visual countdowns to mandatory 72-hour maintenance limits.
* **Clinical Blackout Mode:** Suppresses routine notifications prior to scheduled medical baseline testing.
* **Automated Disaster Recovery:** Integrated `systemd` timers for daily `.gz` database snapshots with a 365-day rolling retention policy.
* **Stealth Mode:** Hardware hotkey (`Esc`) UI obfuscation.
EOF
