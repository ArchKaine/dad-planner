using System;
using System.IO;
using System.Text.Json;
using System.Timers;
using System.Diagnostics;
using System.Collections.Generic;
using Photino.NET;
using Microsoft.Data.Sqlite;

namespace WankPlanner
{
    class Program
    {
        static string dbPath = "inventory.db";
        static System.Timers.Timer? bgTimer;

        [STAThread]
        static void Main(string[] args)
        {
            InitializeDatabase();

            if (args.Length > 0 && args[0] == "--log")
            {
                LogEvent(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "Maintenance", "Normal", 0);
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

                var doc = JsonDocument.Parse(message);
                string action = doc.RootElement.GetProperty("action").GetString() ?? "";

                if (action == "RESIZE_WINDOW")
                {
                    int width = doc.RootElement.GetProperty("width").GetInt32();
                    int height = doc.RootElement.GetProperty("height").GetInt32();
                    w.SetSize(width, height);
                    return; 
                }

                using var db = new SqliteConnection($"Data Source={dbPath}");
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
                    
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    CheckFloorThreshold(db, now);
                    LogEvent(now, mode, volume, heatFlag);
                    SendState(w, db);
                }
                else if (action == "MANUAL_LOG")
                {
                    long ts = doc.RootElement.GetProperty("timestamp").GetInt64();
                    string mode = doc.RootElement.GetProperty("mode").GetString() ?? "Maintenance";
                    string volume = doc.RootElement.GetProperty("volume").GetString() ?? "Normal";
                    int heatFlag = doc.RootElement.GetProperty("heatFlag").GetInt32();
                    
                    LogEvent(ts, mode, volume, heatFlag);
                    SendState(w, db);
                }
                else if (action == "UPDATE_LOG")
                {
                    long id = doc.RootElement.GetProperty("id").GetInt64();
                    long ts = doc.RootElement.GetProperty("timestamp").GetInt64();
                    string mode = doc.RootElement.GetProperty("mode").GetString() ?? "Maintenance";
                    string volume = doc.RootElement.GetProperty("volume").GetString() ?? "Normal";
                    int heatFlag = doc.RootElement.GetProperty("heatFlag").GetInt32();
                    
                    using var cmd = db.CreateCommand();
                    cmd.CommandText = "UPDATE Logs SET Timestamp = $ts, Mode = $mode, Volume = $vol, HeatFlag = $heat WHERE Id = $id";
                    cmd.Parameters.AddWithValue("$ts", ts);
                    cmd.Parameters.AddWithValue("$mode", mode);
                    cmd.Parameters.AddWithValue("$vol", volume);
                    cmd.Parameters.AddWithValue("$heat", heatFlag);
                    cmd.Parameters.AddWithValue("$id", id); // <-- This was the missing line
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
                        restoreCmd.CommandText = "DROP TABLE Logs; ALTER TABLE Logs_Backup RENAME TO Logs;";
                        restoreCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        using var backupCmd = db.CreateCommand();
                        backupCmd.CommandText = "ALTER TABLE Logs RENAME TO Logs_Backup; CREATE TABLE Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER, Mode TEXT DEFAULT 'Maintenance', Volume TEXT DEFAULT 'Normal', HeatFlag INTEGER DEFAULT 0);";
                        backupCmd.ExecuteNonQuery();

                        long currentTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var rand = new Random();
                        string[] modes = { "Maintenance", "Playtime", "Baby-Making" };
                        string[] volumes = { "Low", "Normal", "High" };

                        for (int i = 0; i < 20; i++)
                        {
                            int gapHours = rand.Next(20, 85);
                            currentTs -= (gapHours * 3600);
                            string randomMode = modes[rand.Next(modes.Length)];
                            string randomVol = randomMode == "Maintenance" ? volumes[rand.Next(volumes.Length)] : "N/A";
                            int randomHeat = rand.Next(100) > 85 ? 1 : 0; 
                            
                            using var insertCmd = db.CreateCommand();
                            insertCmd.CommandText = "INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag) VALUES ($ts, $mode, $vol, $heat)";
                            insertCmd.Parameters.AddWithValue("$ts", currentTs);
                            insertCmd.Parameters.AddWithValue("$mode", randomMode);
                            insertCmd.Parameters.AddWithValue("$vol", randomVol);
                            insertCmd.Parameters.AddWithValue("$heat", randomHeat);
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                    SendState(w, db);
                }
            });

            bgTimer = new System.Timers.Timer(15 * 60 * 1000);
            bgTimer.Elapsed += CheckCeilingThreshold;
            bgTimer.Start();

            window.WaitForClose();
        }

        static void InitializeDatabase()
        {
            using var db = new SqliteConnection($"Data Source={dbPath}");
            db.Open();
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
        }

        static void LogEvent(long timestamp, string mode, string volume, int heatFlag)
        {
            using var db = new SqliteConnection($"Data Source={dbPath}");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag) VALUES ($ts, $mode, $vol, $heat)";
            cmd.Parameters.AddWithValue("$ts", timestamp);
            cmd.Parameters.AddWithValue("$mode", mode);
            cmd.Parameters.AddWithValue("$vol", volume);
            cmd.Parameters.AddWithValue("$heat", heatFlag);
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
            using var db = new SqliteConnection($"Data Source={dbPath}");
            db.Open();

            using var cmdAppt = db.CreateCommand();
            cmdAppt.CommandText = "SELECT Timestamp FROM Appointments ORDER BY Timestamp DESC LIMIT 1";
            var apptResult = cmdAppt.ExecuteScalar();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (apptResult != null)
            {
                long apptTs = Convert.ToInt64(apptResult);
                if (apptTs > now && (apptTs - now) <= (5 * 24 * 3600))
                {
                    return; 
                }
            }

            using var cmdLog = db.CreateCommand();
            cmdLog.CommandText = "SELECT Timestamp FROM Logs ORDER BY Timestamp DESC LIMIT 1";
            var logResult = cmdLog.ExecuteScalar();

            if (logResult != null)
            {
                long lastTimestamp = Convert.ToInt64(logResult);
                double hoursSinceLast = (now - lastTimestamp) / 3600.0;
                double maxThreshold = GetSetting(db, "max_threshold", 72.0);

                if (hoursSinceLast > maxThreshold)
                {
                    ShowNotification("Maintenance Overdue", $"Routine turnover limit of {maxThreshold}h has been exceeded. ({hoursSinceLast:F1}h elapsed)");
                }
            }
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
            cmdLogs.CommandText = "SELECT Id, Timestamp, IFNULL(Mode, 'Maintenance'), IFNULL(Volume, 'Normal'), IFNULL(HeatFlag, 0) FROM Logs ORDER BY Timestamp DESC";
            using var reader = cmdLogs.ExecuteReader();
            
            var logs = new List<object>();
            while (reader.Read())
            {
                logs.Add(new { 
                    id = reader.GetInt64(0), 
                    timestamp = reader.GetInt64(1),
                    mode = reader.GetString(2),
                    volume = reader.GetString(3),
                    heatFlag = reader.GetInt32(4)
                });
            }

            using var cmdAppt = db.CreateCommand();
            cmdAppt.CommandText = "SELECT Timestamp FROM Appointments ORDER BY Timestamp DESC LIMIT 1";
            var apptResult = cmdAppt.ExecuteScalar();
            long appt = apptResult != null ? Convert.ToInt64(apptResult) : 0;

            double min = GetSetting(db, "min_threshold", 24.0);
            double max = GetSetting(db, "max_threshold", 72.0);

            using var checkTestCmd = db.CreateCommand();
            checkTestCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Logs_Backup'";
            bool isTestMode = checkTestCmd.ExecuteScalar() != null;

            var payload = new
            {
                action = "UPDATE_STATE",
                logs = logs,
                appointment = appt,
                settings = new { min = min, max = max },
                isTestMode = isTestMode
            };

            window.SendWebMessage(JsonSerializer.Serialize(payload));
        }

        static void ShowNotification(string title, string message)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"\"{title}\" \"{message}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch { }
        }
    }
}
