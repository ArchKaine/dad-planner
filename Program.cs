using System;
using System.IO;
using System.Text.Json;
using System.Timers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Photino.NET;
using Microsoft.Data.Sqlite;

namespace WankPlanner
{
    class Program
    {
        static string dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PIMS");
        static string dbPath = Path.Combine(dbDir, "inventory.db");
        
        // Windows Fix 1: Disable connection pooling to force the OS to release file locks instantly
        static string dbConnectionString = $"Data Source={dbPath};Pooling=False;";
        static System.Timers.Timer? bgTimer;

        [STAThread]
        static void Main(string[] args)
        {
            Directory.CreateDirectory(dbDir);

            // Legacy DB Migration
            string legacyDb = "inventory.db";
            string altLegacyDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");
            
            if (!File.Exists(dbPath))
            {
                try 
                {
                    if (File.Exists(legacyDb)) File.Move(legacyDb, dbPath);
                    else if (File.Exists(altLegacyDb)) File.Copy(altLegacyDb, dbPath, true);
                }
                catch { /* Failsafe */ }
            }

            // Windows Fix 2: Aggressively nuke ALL restrictive file attributes (ReadOnly, Hidden, System)
            if (File.Exists(dbPath))
            {
                try
                {
                    File.SetAttributes(dbPath, FileAttributes.Normal);
                }
                catch { }
            }

            InitializeDatabase();

            if (args.Length > 0 && args[0] == "--log")
            {
                using var headlessDb = new SqliteConnection(dbConnectionString);
                headlessDb.Open();
                LogEvent(headlessDb, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "Maintenance", "Normal", 0, 0, 0);
                return;
            }

            var window = new PhotinoWindow()
                .SetTitle("System Maintenance Utility")
                .SetUseOsDefaultSize(false)
                .SetSize(900, 800) 
                .Center()
                .Load("wwwroot/index.html");

            window.RegisterWebMessageReceivedHandler((object? sender, string message) =>
            {
                if (sender is not PhotinoWindow w) return;

                try
                {
                    var doc = JsonDocument.Parse(message);
                    string action = doc.RootElement.GetProperty("action").GetString() ?? "";

                    if (action == "RESIZE_WINDOW")
                    {
                        int width = doc.RootElement.GetProperty("width").GetInt32();
                        int height = doc.RootElement.GetProperty("height").GetInt32();
                        w.SetSize(width, height);
                        return; 
                    }

                    using var db = new SqliteConnection(dbConnectionString);
                    db.Open();

                    if (action == "GET_STATE")
                    {
                        SendState(w, db);
                    }
                    else if (action == "LOG_EVENT")
                    {
                        string mode = doc.RootElement.GetProperty("mode").GetString() ?? "Maintenance";
                        string volume = doc.RootElement.GetProperty("volume").GetString() ?? "Normal";
                        int heatFlag = doc.RootElement.GetProperty("heatFlag").GetInt32();
                        int zincFlag = doc.RootElement.GetProperty("zincFlag").GetInt32();
                        int macaFlag = doc.RootElement.GetProperty("macaFlag").GetInt32();
                        
                        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        CheckFloorThreshold(db, now);
                        LogEvent(db, now, mode, volume, heatFlag, zincFlag, macaFlag);
                        SendState(w, db);
                    }
                    else if (action == "MANUAL_LOG")
                    {
                        long ts = doc.RootElement.GetProperty("timestamp").GetInt64();
                        string mode = doc.RootElement.GetProperty("mode").GetString() ?? "Maintenance";
                        string volume = doc.RootElement.GetProperty("volume").GetString() ?? "Normal";
                        int heatFlag = doc.RootElement.GetProperty("heatFlag").GetInt32();
                        int zincFlag = doc.RootElement.GetProperty("zincFlag").GetInt32();
                        int macaFlag = doc.RootElement.GetProperty("macaFlag").GetInt32();
                        
                        LogEvent(db, ts, mode, volume, heatFlag, zincFlag, macaFlag);
                        SendState(w, db);
                    }
                    else if (action == "UPDATE_LOG")
                    {
                        long id = doc.RootElement.GetProperty("id").GetInt64();
                        long ts = doc.RootElement.GetProperty("timestamp").GetInt64();
                        string mode = doc.RootElement.GetProperty("mode").GetString() ?? "Maintenance";
                        string volume = doc.RootElement.GetProperty("volume").GetString() ?? "Normal";
                        int heatFlag = doc.RootElement.GetProperty("heatFlag").GetInt32();
                        int zincFlag = doc.RootElement.GetProperty("zincFlag").GetInt32();
                        int macaFlag = doc.RootElement.GetProperty("macaFlag").GetInt32();
                        
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = "UPDATE Logs SET Timestamp = $ts, Mode = $mode, Volume = $vol, HeatFlag = $heat, ZincFlag = $zinc, MacaFlag = $maca WHERE Id = $id";
                        cmd.Parameters.AddWithValue("$ts", ts);
                        cmd.Parameters.AddWithValue("$mode", mode);
                        cmd.Parameters.AddWithValue("$vol", volume);
                        cmd.Parameters.AddWithValue("$heat", heatFlag);
                        cmd.Parameters.AddWithValue("$zinc", zincFlag);
                        cmd.Parameters.AddWithValue("$maca", macaFlag);
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                        SendState(w, db);
                    }
                    else if (action == "DELETE_LOG")
                    {
                        long id = doc.RootElement.GetProperty("id").GetInt64();
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = "DELETE FROM Logs WHERE Id = $id";
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                        SendState(w, db);
                    }
                    else if (action == "SET_APPOINTMENT")
                    {
                        long ts = doc.RootElement.GetProperty("timestamp").GetInt64();
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = "INSERT INTO Appointments (Timestamp) VALUES ($ts)";
                        cmd.Parameters.AddWithValue("$ts", ts);
                        cmd.ExecuteNonQuery();
                        SendState(w, db);
                    }
                    else if (action == "CLEAR_APPOINTMENT")
                    {
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = "DELETE FROM Appointments";
                        cmd.ExecuteNonQuery();
                        SendState(w, db);
                    }
                    else if (action == "SAVE_SETTINGS")
                    {
                        double min = doc.RootElement.GetProperty("min").GetDouble();
                        double max = doc.RootElement.GetProperty("max").GetDouble();
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('min_threshold', $min), ('max_threshold', $max)";
                        cmd.Parameters.AddWithValue("$min", min.ToString());
                        cmd.Parameters.AddWithValue("$max", max.ToString());
                        cmd.ExecuteNonQuery();
                        SendState(w, db);
                    }
                    else if (action == "TOGGLE_TEST_MODE")
                    {
                        using var checkCmd = db.CreateCommand();
                        checkCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Logs_Backup'";
                        bool isTestMode = checkCmd.ExecuteScalar() != null;

                        if (isTestMode)
                        {
                            using var restoreCmd = db.CreateCommand();
                            restoreCmd.CommandText = @"
                                DROP TABLE IF EXISTS Logs;
                                DROP TABLE IF EXISTS Settings;
                                DROP TABLE IF EXISTS Appointments;
                                ALTER TABLE Logs_Backup RENAME TO Logs;
                                ALTER TABLE Settings_Backup RENAME TO Settings;
                                ALTER TABLE Appointments_Backup RENAME TO Appointments;
                            ";
                            restoreCmd.ExecuteNonQuery();
                        }
                        else
                        {
                            using var backupCmd = db.CreateCommand();
                            backupCmd.CommandText = @"
                                ALTER TABLE Logs RENAME TO Logs_Backup;
                                ALTER TABLE Settings RENAME TO Settings_Backup;
                                ALTER TABLE Appointments RENAME TO Appointments_Backup;
                                
                                CREATE TABLE Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER, Mode TEXT DEFAULT 'Maintenance', Volume TEXT DEFAULT 'Normal', HeatFlag INTEGER DEFAULT 0, ZincFlag INTEGER DEFAULT 0, MacaFlag INTEGER DEFAULT 0);
                                
                                CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT);
                                INSERT INTO Settings SELECT * FROM Settings_Backup;
                                
                                CREATE TABLE Appointments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER);
                            ";
                            backupCmd.ExecuteNonQuery();

                            long currentTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var rand = new Random();
                            string[] modes = { "Maintenance", "Playtime", "Baby-Making" };
                            string[] volumes = { "Low", "Normal", "High" };
                            
                            int testRecordCount = 150;
                            int singleHeatEventIndex = rand.Next(0, testRecordCount);

                            for (int i = 0; i < testRecordCount; i++)
                            {
                                int randomZinc = (i < 75) ? 1 : 0;
                                int randomMaca = (i < 75) ? 1 : 0;
                                
                                int gapHours = rand.Next(35, 80);
                                if (randomMaca == 1) gapHours -= rand.Next(10, 20); 
                                
                                currentTs -= (gapHours * 3600);
                                string randomMode = modes[rand.Next(modes.Length)];
                                
                                string randomVol = "N/A";
                                if (randomMode == "Maintenance")
                                {
                                    if (randomZinc == 1) 
                                    {
                                        randomVol = rand.Next(100) > 15 ? "High" : "Normal"; 
                                    }
                                    else 
                                    {
                                        int spread = rand.Next(100);
                                        randomVol = spread < 40 ? "Low" : (spread < 80 ? "Normal" : "High");
                                    }
                                }
                                
                                int randomHeat = (i == singleHeatEventIndex) ? 1 : 0; 
                                
                                using var insertCmd = db.CreateCommand();
                                insertCmd.CommandText = "INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag, ZincFlag, MacaFlag) VALUES ($ts, $mode, $vol, $heat, $zinc, $maca)";
                                insertCmd.Parameters.AddWithValue("$ts", currentTs);
                                insertCmd.Parameters.AddWithValue("$mode", randomMode);
                                insertCmd.Parameters.AddWithValue("$vol", randomVol);
                                insertCmd.Parameters.AddWithValue("$heat", randomHeat);
                                insertCmd.Parameters.AddWithValue("$zinc", randomZinc);
                                insertCmd.Parameters.AddWithValue("$maca", randomMaca);
                                insertCmd.ExecuteNonQuery();
                            }
                        }
                        SendState(w, db);
                    }
                }
                catch (Exception ex)
                {
                    // Windows Fix 3: Intercept silent UI crashes and blast them to the desktop notifications so we can see the exact error.
                    ShowNotification("PIMS Database Error", ex.Message);
                }
            });

            bgTimer = new System.Timers.Timer(15 * 60 * 1000);
            bgTimer.Elapsed += CheckCeilingThreshold;
            bgTimer.Start();

            window.WaitForClose();
        }

        static void InitializeDatabase()
        {
            try
            {
                using var db = new SqliteConnection(dbConnectionString);
                db.Open();

                using var walCmd = db.CreateCommand();
                walCmd.CommandText = "PRAGMA journal_mode=WAL;";
                walCmd.ExecuteNonQuery();

                using var cmd = db.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER);
                    CREATE TABLE IF NOT EXISTS Appointments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER);
                    CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT);
                    INSERT OR IGNORE INTO Settings (Key, Value) VALUES ('min_threshold', '24'), ('max_threshold', '72');
                ";
                cmd.ExecuteNonQuery();

                try { using var m1 = db.CreateCommand(); m1.CommandText = "ALTER TABLE Logs ADD COLUMN Mode TEXT DEFAULT 'Maintenance'"; m1.ExecuteNonQuery(); } catch { }
                try { using var m2 = db.CreateCommand(); m2.CommandText = "ALTER TABLE Logs ADD COLUMN Volume TEXT DEFAULT 'Normal'"; m2.ExecuteNonQuery(); } catch { }
                try { using var m3 = db.CreateCommand(); m3.CommandText = "ALTER TABLE Logs ADD COLUMN HeatFlag INTEGER DEFAULT 0"; m3.ExecuteNonQuery(); } catch { }
                try { using var m4 = db.CreateCommand(); m4.CommandText = "ALTER TABLE Logs ADD COLUMN ZincFlag INTEGER DEFAULT 0"; m4.ExecuteNonQuery(); } catch { }
                try { using var m5 = db.CreateCommand(); m5.CommandText = "ALTER TABLE Logs ADD COLUMN MacaFlag INTEGER DEFAULT 0"; m5.ExecuteNonQuery(); } catch { }
            }
            catch (Exception ex)
            {
                ShowNotification("Init Error", ex.Message);
            }
        }

        static void LogEvent(SqliteConnection db, long timestamp, string mode, string volume, int heatFlag, int zincFlag, int macaFlag)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag, ZincFlag, MacaFlag) VALUES ($ts, $mode, $vol, $heat, $zinc, $maca)";
            cmd.Parameters.AddWithValue("$ts", timestamp);
            cmd.Parameters.AddWithValue("$mode", mode);
            cmd.Parameters.AddWithValue("$vol", volume);
            cmd.Parameters.AddWithValue("$heat", heatFlag);
            cmd.Parameters.AddWithValue("$zinc", zincFlag);
            cmd.Parameters.AddWithValue("$maca", macaFlag);
            cmd.ExecuteNonQuery();
        }

        static void CheckFloorThreshold(SqliteConnection db, long newTimestamp)
        {
            using var cmdLog = db.CreateCommand();
            cmdLog.CommandText = "SELECT Timestamp FROM Logs ORDER BY Timestamp DESC LIMIT 1";
            var result = cmdLog.ExecuteScalar();
            if (result != null)
            {
                long lastTimestamp = Convert.ToInt64(result);
                double hoursSinceLast = (newTimestamp - lastTimestamp) / 3600.0;
                double minThreshold = GetSetting(db, "min_threshold", 24.0);
                if (hoursSinceLast < minThreshold)
                {
                    ShowNotification("Warning: Minimum Rest Period Breached", $"Only {hoursSinceLast:F1}h elapsed (Target: {minThreshold}h). Potential volume depletion.");
                }
            }
        }

        static void CheckCeilingThreshold(object? sender, ElapsedEventArgs e)
        {
            try
            {
                using var db = new SqliteConnection(dbConnectionString);
                db.Open();
                using var cmdAppt = db.CreateCommand();
                cmdAppt.CommandText = "SELECT Timestamp FROM Appointments ORDER BY Timestamp DESC LIMIT 1";
                var apptResult = cmdAppt.ExecuteScalar();
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (apptResult != null)
                {
                    long apptTs = Convert.ToInt64(apptResult);
                    if (apptTs > now && (apptTs - now) <= (5 * 24 * 3600)) return; 
                }

                using var cmdLog = db.CreateCommand();
                cmdLog.CommandText = "SELECT Timestamp, IFNULL(Volume, 'Normal') FROM Logs ORDER BY Timestamp DESC LIMIT 1";
                using var reader = cmdLog.ExecuteReader();
                if (reader.Read())
                {
                    long lastTimestamp = reader.GetInt64(0);
                    string lastVolume = reader.GetString(1);
                    double hoursSinceLast = (now - lastTimestamp) / 3600.0;
                    double maxThreshold = GetSetting(db, "max_threshold", 72.0);

                    if (lastVolume == "Low")
                    {
                        maxThreshold += 24.0;
                    }

                    if (hoursSinceLast > maxThreshold)
                    {
                        ShowNotification("Maintenance Overdue", $"Routine turnover limit of {maxThreshold}h has been exceeded. ({hoursSinceLast:F1}h elapsed)");
                    }
                }
            }
            catch { }
        }

        static double GetSetting(SqliteConnection db, string key, double defaultValue)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
            cmd.Parameters.AddWithValue("$key", key);
            var result = cmd.ExecuteScalar();
            if (result != null && double.TryParse(result.ToString(), out double val)) return val;
            return defaultValue;
        }

        static void SendState(PhotinoWindow window, SqliteConnection db)
        {
            using var cmdLogs = db.CreateCommand();
            cmdLogs.CommandText = "SELECT Id, Timestamp, IFNULL(Mode, 'Maintenance'), IFNULL(Volume, 'Normal'), IFNULL(HeatFlag, 0), IFNULL(ZincFlag, 0), IFNULL(MacaFlag, 0) FROM Logs ORDER BY Timestamp DESC";
            using var reader = cmdLogs.ExecuteReader();
            
            var logs = new List<object>();
            while (reader.Read())
            {
                logs.Add(new { 
                    id = reader.GetInt64(0), 
                    timestamp = reader.GetInt64(1),
                    mode = reader.GetString(2),
                    volume = reader.GetString(3),
                    heatFlag = reader.GetInt32(4),
                    zincFlag = reader.GetInt32(5),
                    macaFlag = reader.GetInt32(6)
                });
            }

            using var cmdAppt = db.CreateCommand();
            cmdAppt.CommandText = "SELECT Timestamp FROM Appointments ORDER BY Timestamp DESC LIMIT 1";
            var apptResult = cmdAppt.ExecuteScalar();
            long appt = apptResult != null ? Convert.ToInt64(apptResult) : 0;
            double min = GetSetting(db, "min_threshold", 24.0);
            double max = GetSetting(db, "max_threshold", 72.0);

            bool isOverdue = false;
            string currentVol = logs.Count > 0 ? ((dynamic)logs[0]).volume : "Normal";
            double activeMax = (currentVol == "Low") ? max + 24.0 : max;
            
            if (logs.Count > 0)
            {
                long lastTimestamp = ((dynamic)logs[0]).timestamp;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                double hoursSinceLast = (now - lastTimestamp) / 3600.0;

                if (hoursSinceLast > activeMax)
                {
                    isOverdue = true;
                }
            }

            using var checkTestCmd = db.CreateCommand();
            checkTestCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Logs_Backup'";
            bool isTestMode = checkTestCmd.ExecuteScalar() != null;

            var payload = new
            {
                action = "UPDATE_STATE",
                logs = logs,
                appointment = appt,
                settings = new { 
                    min = min, 
                    max = max,
                    activeMax = activeMax,
                    isOverdue = isOverdue
                },
                isTestMode = isTestMode
            };

            window.SendWebMessage(JsonSerializer.Serialize(payload));
        }

        static void ShowNotification(string title, string message)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string psCommand = $"Add-Type -AssemblyName System.Windows.Forms; $n = New-Object System.Windows.Forms.NotifyIcon; $n.Icon = [System.Drawing.SystemIcons]::Information; $n.Visible = $true; $n.ShowBalloonTip(5000, '{title}', '{message}', 'Info'); Start-Sleep -Seconds 5; $n.Dispose()";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-WindowStyle Hidden -Command \"{psCommand}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "osascript",
                        Arguments = $"-e 'display notification \"{message}\" with title \"{title}\"'",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notify-send",
                        Arguments = $"\"{title}\" \"{message}\"",
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch { /* Failsafe */ }
        }
    }
}
