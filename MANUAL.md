# PIMS: Operational Manual

## 1. Core Philosophy
PIMS operates on the principle of passive data collection. It rejects the use of active stopwatches or timers, which trigger the sympathetic nervous system (stress) and mechanically degrade performance. Instead, it relies on time-stamped interval tracking (the gap between events) and visual output metrics (volume) to chart physical limits and system efficiency. 

## 2. The HUD & Telemetry
The main dashboard tracks four real-time metrics:
* **Current Delta:** Hours elapsed since the last logged event. Turns orange if you breach the Minimum Rest floor.
* **Limit T-Minus:** Hours remaining until the system reaches the maximum limit (senescence/degradation risk). Reads "OVERDUE" in red if breached.
* **Rolling Avg:** Your mean recovery time across all recorded logs.
* **Max Endurance:** The longest gap you have mathematically sustained between events.

## 3. Event Modes
Logs are categorized into three operational modes:
* **Maintenance:** Standard solo cycle turnover. The only mode where volume is accurately trackable.
* **Playtime:** Casual partner event. Volume is not tracked (N/A) due to tracking limitations during the event.
* **Baby-Making:** Targeted partner event optimized for conception windows. 

## 4. Pre-Log Modifiers (The Variables)
Before clicking a log button, variables can be toggled to provide context for the Efficacy Analysis engine.
* **Fever / Hot Tub:** Flags the event as a thermal anomaly. The Auto-Calibrate engine will ignore this log when calculating your baseline, as heat temporarily halts spermatogenesis.
* **Volume (Low / Normal / High):** A subjective visual proxy for density and yield. 
* **Zinc:** A foundational mineral for prostatic fluid. Tracks correlation to volume yields.
* **Maca Root:** An endocrine adaptogen. Tracks correlation to recovery speed (reduced interval gaps).
*(Note: Supplement toggles remain checked after logging to accommodate daily usage routines).*

## 5. Analytics & Configuration (⚙️ Settings)
Access the Configuration Modal via the top right corner.

**Auto-Calibrate Limits:**
Analyzes your historical data (excluding Fever anomalies) to establish your mechanical limits:
* **Floor (Min Rest):** Calculates the exact hour-mark where you consistently produce 'Normal' or 'High' volume.
* **Ceiling (Max Limit):** Calculates your behavioral average plus one standard deviation, clamped by the medical hard-limit of 120 hours (5 days) before oxidative stress degrades the sample.

**Analyze Supplements:**
Runs a statistical comparison between your un-supplemented baseline and your supplemented logs:
* **Zinc Test:** Calculates the percentage increase/decrease in 'High' and 'Normal' volume yields.
* **Maca Test:** Calculates the mathematical difference in mean recovery hours.

**Clinical Blackout Window:**
Allows you to set a future target date. This triggers a persistent banner counting down to the event, allowing you to mathematically align your 48-hour recovery rhythm to ensure peak supply and freshness for the target date.

## 6. Debug & Sandbox
The Settings modal includes a toggle for **Test Mode**. 
Activating this securely backs up your real `inventory.db` and generates 150 simulated records (roughly 6 months of data). The generator simulates a clean 3-month un-supplemented baseline, followed by a 3-month fully saturated Zinc/Maca period. Use this to safely test the Auto-Calibrate and Supplement Analysis math before reverting to your live data.
