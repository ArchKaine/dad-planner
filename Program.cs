using System;
using System.IO;
using System.Text.Json;
using System.Timers;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Photino.NET;
using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DadPlanner
{
    class Program
    {
        static string dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PIMS");
        static string dbPath = Path.Combine(dbDir, "inventory.db");
        
        static string dbConnectionString = $"Data Source={dbPath};Pooling=False;";
        static System.Timers.Timer? bgTimer;

        static bool _alertShownForCurrentCycle = false;
        static long _lastKnownLogTime = 0;

        [STAThread]
        static void Main(string[] args)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            Directory.CreateDirectory(dbDir);

            string legacyDb = "inventory.db";
            string altLegacyDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");
            
            if (!File.Exists(dbPath))
            {
                try 
                {
                    if (File.Exists(legacyDb)) File.Move(legacyDb, dbPath);
                    else if (File.Exists(altLegacyDb)) File.Copy(altLegacyDb, dbPath, true);
                }
                catch { }
            }

            if (File.Exists(dbPath))
            {
                try { File.SetAttributes(dbPath, FileAttributes.Normal); } catch { }
            }

            InitializeDatabase();

            if (args.Length > 0 && args[0] == "--log")
            {
                using var headlessDb = new SqliteConnection(dbConnectionString);
                headlessDb.Open();
                LogEvent(headlessDb, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "Maintenance", "Normal", 0, "{}");
                return;
            }

            var window = new PhotinoWindow()
                .SetTitle("System Maintenance Utility")
                .SetUseOsDefaultSize(false)
                .SetSize(1280, 850) 
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
                    else if (action == "GENERATE_PDF")
                    {
                        Generate90DayReport(db);
                    }
                    else if (action == "OPEN_LAB_REPORT")
                    {
                        long id = doc.RootElement.GetProperty("id").GetInt64();
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = "SELECT LabReportBlob, LabReportFileName FROM Logs WHERE Id = $id AND LabReportBlob IS NOT NULL";
                        cmd.Parameters.AddWithValue("$id", id);
                        using var reader = cmd.ExecuteReader();
                        
                        if (reader.Read())
                        {
                            byte[] pdfBytes = (byte[])reader["LabReportBlob"];
                            string fileName = reader["LabReportFileName"].ToString() ?? $"LabReport_{id}.pdf";
                            
                            string tempPath = Path.Combine(Path.GetTempPath(), fileName);
                            File.WriteAllBytes(tempPath, pdfBytes);
                            
                            try
                            {
                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                    Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                    Process.Start("xdg-open", tempPath);
                                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                    Process.Start("open", tempPath);
                            }
                            catch (Exception ex) { ShowNotification("Error Opening PDF", ex.Message); }
                        }
                    }
                    else if (action == "LOG_EVENT" || action == "MANUAL_LOG")
                    {
                        long ts = action == "MANUAL_LOG" 
                            ? doc.RootElement.GetProperty("timestamp").GetInt64() 
                            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                        string mode = doc.RootElement.TryGetProperty("mode", out var mProp) ? mProp.GetString() ?? "Maintenance" : "Maintenance";
                        string volume = doc.RootElement.TryGetProperty("volume", out var vProp) ? vProp.GetString() ?? "Normal" : "Normal";
                        
                        int heatFlag = doc.RootElement.TryGetProperty("heatFlag", out var hProp) ? hProp.GetInt32() : 0;
                        string supplements = doc.RootElement.TryGetProperty("supplements", out var suppProp) ? suppProp.GetString() ?? "{}" : "{}";

                        int conc = doc.RootElement.TryGetProperty("concentration", out var cProp) && cProp.ValueKind == JsonValueKind.Number ? cProp.GetInt32() : 0;
                        int mot = doc.RootElement.TryGetProperty("motility", out var motProp) && motProp.ValueKind == JsonValueKind.Number ? motProp.GetInt32() : 0;
                        int morph = doc.RootElement.TryGetProperty("morphology", out var morphProp) && morphProp.ValueKind == JsonValueKind.Number ? morphProp.GetInt32() : 0;
                        
                        double clinicalVol = doc.RootElement.TryGetProperty("clinicalVol", out var cvProp) && cvProp.ValueKind == JsonValueKind.Number ? cvProp.GetDouble() : 0.0;
                        int progMot = doc.RootElement.TryGetProperty("progMotility", out var pmProp) && pmProp.ValueKind == JsonValueKind.Number ? pmProp.GetInt32() : 0;
                        double ph = doc.RootElement.TryGetProperty("phLevel", out var phProp) && phProp.ValueKind == JsonValueKind.Number ? phProp.GetDouble() : 0.0;

                        string? labFileName = doc.RootElement.TryGetProperty("labFileName", out var fnProp) ? fnProp.GetString() : null;
                        string? labFileData = doc.RootElement.TryGetProperty("labFileData", out var fdProp) ? fdProp.GetString() : null;
                        byte[]? labBlob = !string.IsNullOrEmpty(labFileData) ? Convert.FromBase64String(labFileData) : null;
                        
                        if (action == "LOG_EVENT" && volume != "None" && volume != "N/A") CheckFloorThreshold(db, ts);
                        
                        LogEvent(db, ts, mode, volume, heatFlag, supplements, conc, mot, morph, clinicalVol, progMot, ph, labFileName, labBlob);
                        SendState(w, db);
                    }
                    else if (action == "UPDATE_LOG")
                    {
                        long id = doc.RootElement.GetProperty("id").GetInt64();
                        long ts = doc.RootElement.GetProperty("timestamp").GetInt64();
                        string mode = doc.RootElement.GetProperty("mode").GetString() ?? "Maintenance";
                        string volume = doc.RootElement.GetProperty("volume").GetString() ?? "Normal";
                        int heatFlag = doc.RootElement.GetProperty("heatFlag").GetInt32();
                        string supplements = doc.RootElement.TryGetProperty("supplements", out var suppProp) ? suppProp.GetString() ?? "{}" : "{}";

                        double clinicalVol = doc.RootElement.TryGetProperty("clinicalVol", out var cvProp) && cvProp.ValueKind == JsonValueKind.Number ? cvProp.GetDouble() : 0.0;
                        int conc = doc.RootElement.TryGetProperty("concentration", out var cProp) && cProp.ValueKind == JsonValueKind.Number ? cProp.GetInt32() : 0;
                        int mot = doc.RootElement.TryGetProperty("motility", out var motProp) && motProp.ValueKind == JsonValueKind.Number ? motProp.GetInt32() : 0;
                        int pmot = doc.RootElement.TryGetProperty("progMotility", out var pmProp) && pmProp.ValueKind == JsonValueKind.Number ? pmProp.GetInt32() : 0;
                        int morph = doc.RootElement.TryGetProperty("morphology", out var morphProp) && morphProp.ValueKind == JsonValueKind.Number ? morphProp.GetInt32() : 0;
                        double ph = doc.RootElement.TryGetProperty("phLevel", out var phProp) && phProp.ValueKind == JsonValueKind.Number ? phProp.GetDouble() : 0.0;
                        
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = @"
                            UPDATE Logs SET 
                                Timestamp = $ts, Mode = $mode, Volume = $vol, 
                                HeatFlag = $heat, Supplements = $supps, 
                                ClinicalVol = $cvol, Concentration = $conc, Motility = $mot, 
                                ProgMotility = $pmot, Morphology = $morph, PhLevel = $ph 
                            WHERE Id = $id";

                        cmd.Parameters.AddWithValue("$ts", ts);
                        cmd.Parameters.AddWithValue("$mode", mode);
                        cmd.Parameters.AddWithValue("$vol", volume);
                        cmd.Parameters.AddWithValue("$heat", heatFlag);
                        cmd.Parameters.AddWithValue("$supps", supplements);
                        cmd.Parameters.AddWithValue("$cvol", clinicalVol);
                        cmd.Parameters.AddWithValue("$conc", conc);
                        cmd.Parameters.AddWithValue("$mot", mot);
                        cmd.Parameters.AddWithValue("$pmot", pmot);
                        cmd.Parameters.AddWithValue("$morph", morph);
                        cmd.Parameters.AddWithValue("$ph", ph);
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                        
                        _alertShownForCurrentCycle = false; 
                        SendState(w, db);
                    }
                    else if (action == "DELETE_LOG")
                    {
                        long id = doc.RootElement.GetProperty("id").GetInt64();
                        using var cmd = db.CreateCommand();
                        cmd.CommandText = "DELETE FROM Logs WHERE Id = $id";
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                        
                        _alertShownForCurrentCycle = false;
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
                        
                        _alertShownForCurrentCycle = false;
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
                                
                                CREATE TABLE Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER, Mode TEXT DEFAULT 'Maintenance', Volume TEXT DEFAULT 'Normal', HeatFlag INTEGER DEFAULT 0, Supplements TEXT DEFAULT '{}', Concentration INTEGER, Motility INTEGER, Morphology INTEGER, ClinicalVol REAL, ProgMotility INTEGER, PhLevel REAL, LabReportBlob BLOB, LabReportFileName TEXT);
                                
                                CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT);
                                INSERT INTO Settings SELECT * FROM Settings_Backup;
                                
                                CREATE TABLE Appointments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp INTEGER);
                            ";
                            backupCmd.ExecuteNonQuery();

                            long currentTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var rand = new Random();
                            string[] modes = { "Maintenance", "Playtime", "Baby-Making", "Clinical-Lab" };
                            
                            int testRecordCount = 150;
                            int singleHeatEventIndex = rand.Next(0, testRecordCount);

                            for (int i = 0; i < testRecordCount; i++)
                            {
                                int randomZinc = (i < 75) ? 1 : 0;
                                int randomMaca = (i < 75) ? 1 : 0;
                                int randomVitD = (i < 75) ? 1 : 0;
                                int randomVitC = (i < 75) ? 1 : 0;
                                
                                string fakeSupps = $"{{\"zinc\":{randomZinc},\"maca\":{randomMaca},\"vitD\":{randomVitD},\"vitC\":{randomVitC}}}";
                                
                                int gapHours = rand.Next(35, 80);
                                if (randomMaca == 1) gapHours -= rand.Next(10, 20); 
                                
                                currentTs -= (gapHours * 3600);
                                
                                string randomMode = modes[rand.Next(modes.Length)];
                                if (rand.Next(100) > 5 && randomMode == "Clinical-Lab") randomMode = "Maintenance";

                                string randomVol = "Normal";
                                if (randomZinc == 1) 
                                {
                                    randomVol = rand.Next(100) > 15 ? "High" : "Normal"; 
                                }
                                else 
                                {
                                    int spread = rand.Next(100);
                                    randomVol = spread < 40 ? "Low" : (spread < 80 ? "Normal" : "High");
                                }
                                
                                if (rand.Next(100) > 90 && randomMode != "Baby-Making") randomVol = "None";
                                
                                int randomHeat = (i == singleHeatEventIndex) ? 1 : 0; 

                                int conc = 0, mot = 0, morph = 0, pmot = 0;
                                double cvol = 0.0, ph = 0.0;

                                if (randomMode == "Clinical-Lab")
                                {
                                    cvol = rand.NextDouble() * 3 + 1.5; 
                                    conc = rand.Next(15, 120);
                                    mot = rand.Next(40, 85);
                                    pmot = mot - rand.Next(5, 15);
                                    morph = rand.Next(2, 8);
                                    ph = rand.NextDouble() * 0.8 + 7.2;
                                    randomVol = "High"; 
                                }
                                
                                using var insertCmd = db.CreateCommand();
                                insertCmd.CommandText = "INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag, Supplements, Concentration, Motility, Morphology, ClinicalVol, ProgMotility, PhLevel) VALUES ($ts, $mode, $vol, $heat, $supps, $conc, $mot, $morph, $cvol, $pmot, $ph)";
                                insertCmd.Parameters.AddWithValue("$ts", currentTs);
                                insertCmd.Parameters.AddWithValue("$mode", randomMode);
                                insertCmd.Parameters.AddWithValue("$vol", randomVol);
                                insertCmd.Parameters.AddWithValue("$heat", randomHeat);
                                insertCmd.Parameters.AddWithValue("$supps", fakeSupps);
                                insertCmd.Parameters.AddWithValue("$conc", conc);
                                insertCmd.Parameters.AddWithValue("$mot", mot);
                                insertCmd.Parameters.AddWithValue("$morph", morph);
                                insertCmd.Parameters.AddWithValue("$cvol", cvol);
                                insertCmd.Parameters.AddWithValue("$pmot", pmot);
                                insertCmd.Parameters.AddWithValue("$ph", ph);
                                insertCmd.ExecuteNonQuery();
                            }
                        }
                        
                        _alertShownForCurrentCycle = false;
                        SendState(w, db);
                    }
                }
                catch (Exception ex)
                {
                    // Forcing the stack trace back into the UI so we can see Windows' native library error
                    var errorPayload = new { action = "SHOW_ERROR", message = ex.ToString() };
                    w.SendWebMessage(JsonSerializer.Serialize(errorPayload));
                    
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
                try { using var m6 = db.CreateCommand(); m6.CommandText = "ALTER TABLE Logs ADD COLUMN Concentration INTEGER"; m6.ExecuteNonQuery(); } catch { }
                try { using var m7 = db.CreateCommand(); m7.CommandText = "ALTER TABLE Logs ADD COLUMN Motility INTEGER"; m7.ExecuteNonQuery(); } catch { }
                try { using var m8 = db.CreateCommand(); m8.CommandText = "ALTER TABLE Logs ADD COLUMN Morphology INTEGER"; m8.ExecuteNonQuery(); } catch { }
                try { using var m9 = db.CreateCommand(); m9.CommandText = "ALTER TABLE Logs ADD COLUMN LabReportBlob BLOB"; m9.ExecuteNonQuery(); } catch { }
                try { using var m10 = db.CreateCommand(); m10.CommandText = "ALTER TABLE Logs ADD COLUMN LabReportFileName TEXT"; m10.ExecuteNonQuery(); } catch { }
                try { using var m11 = db.CreateCommand(); m11.CommandText = "ALTER TABLE Logs ADD COLUMN ClinicalVol REAL"; m11.ExecuteNonQuery(); } catch { }
                try { using var m12 = db.CreateCommand(); m12.CommandText = "ALTER TABLE Logs ADD COLUMN ProgMotility INTEGER"; m12.ExecuteNonQuery(); } catch { }
                try { using var m13 = db.CreateCommand(); m13.CommandText = "ALTER TABLE Logs ADD COLUMN PhLevel REAL"; m13.ExecuteNonQuery(); } catch { }
                try { using var m14 = db.CreateCommand(); m14.CommandText = "ALTER TABLE Logs ADD COLUMN Supplements TEXT DEFAULT '{}'"; m14.ExecuteNonQuery(); } catch { }
            }
            catch (Exception ex)
            {
                ShowNotification("Init Error", ex.Message);
            }
        }

        static void LogEvent(SqliteConnection db, long timestamp, string mode, string volume, int heatFlag, string supplements, 
            int conc = 0, int motility = 0, int morphology = 0, double clinicalVol = 0.0, int progMot = 0, double phLevel = 0.0, 
            string? fileName = null, byte[]? blob = null)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Logs (Timestamp, Mode, Volume, HeatFlag, Supplements, Concentration, Motility, Morphology, ClinicalVol, ProgMotility, PhLevel, LabReportFileName, LabReportBlob) 
                VALUES ($ts, $mode, $vol, $heat, $supps, $conc, $mot, $morph, $cvol, $pmot, $ph, $fname, $blob)";
            
            cmd.Parameters.AddWithValue("$ts", timestamp);
            cmd.Parameters.AddWithValue("$mode", mode);
            cmd.Parameters.AddWithValue("$vol", volume);
            cmd.Parameters.AddWithValue("$heat", heatFlag);
            cmd.Parameters.AddWithValue("$supps", supplements);
            cmd.Parameters.AddWithValue("$conc", conc);
            cmd.Parameters.AddWithValue("$mot", motility);
            cmd.Parameters.AddWithValue("$morph", morphology);
            cmd.Parameters.AddWithValue("$cvol", clinicalVol);
            cmd.Parameters.AddWithValue("$pmot", progMot);
            cmd.Parameters.AddWithValue("$ph", phLevel);
            cmd.Parameters.AddWithValue("$fname", fileName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$blob", blob ?? (object)DBNull.Value);
            
            cmd.ExecuteNonQuery();
            _alertShownForCurrentCycle = false;
        }

        static void CheckFloorThreshold(SqliteConnection db, long newTimestamp)
        {
            using var cmdLog = db.CreateCommand();
            cmdLog.CommandText = "SELECT Timestamp FROM Logs WHERE Volume NOT IN ('None', 'N/A') ORDER BY Timestamp DESC LIMIT 1";
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
                cmdLog.CommandText = "SELECT Timestamp, IFNULL(Volume, 'Normal') FROM Logs WHERE Volume NOT IN ('None', 'N/A') ORDER BY Timestamp DESC LIMIT 1";
                using var reader = cmdLog.ExecuteReader();
                
                if (reader.Read())
                {
                    long lastTimestamp = reader.GetInt64(0);
                    string lastVolume = reader.GetString(1);
                    
                    if (lastTimestamp > _lastKnownLogTime)
                    {
                        _lastKnownLogTime = lastTimestamp;
                        _alertShownForCurrentCycle = false; 
                    }

                    double hoursSinceLast = (now - lastTimestamp) / 3600.0;
                    double maxThreshold = GetSetting(db, "max_threshold", 72.0);

                    if (lastVolume == "Low")
                    {
                        maxThreshold += 24.0;
                    }

                    if (hoursSinceLast > maxThreshold)
                    {
                        if (!_alertShownForCurrentCycle)
                        {
                            ShowNotification("Maintenance Overdue", $"Routine turnover limit of {maxThreshold}h has been exceeded. ({hoursSinceLast:F1}h elapsed)");
                            _alertShownForCurrentCycle = true;
                        }
                    }
                    else 
                    {
                        _alertShownForCurrentCycle = false;
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
            cmdLogs.CommandText = "SELECT Id, Timestamp, IFNULL(Mode, 'Maintenance'), IFNULL(Volume, 'Normal'), IFNULL(HeatFlag, 0), IFNULL(Supplements, '{}'), IFNULL(Concentration, 0), IFNULL(Motility, 0), IFNULL(Morphology, 0), LabReportFileName, IFNULL(ClinicalVol, 0.0), IFNULL(ProgMotility, 0), IFNULL(PhLevel, 0.0) FROM Logs ORDER BY Timestamp DESC";
            using var reader = cmdLogs.ExecuteReader();
            
            var logs = new List<object>();
            while (reader.Read())
            {
                logs.Add(new { 
                    id = reader.GetInt64(0), 
                    timestamp = reader.GetInt64(1),
                    mode = reader.GetString(2),
                    volume = reader.GetString(3),
                    heatFlag = Convert.ToInt32(reader.GetValue(4)),
                    supplements = reader.GetString(5),
                    concentration = Convert.ToInt32(reader.GetValue(6)),
                    motility = Convert.ToInt32(reader.GetValue(7)),
                    morphology = Convert.ToInt32(reader.GetValue(8)),
                    hasPdf = !reader.IsDBNull(9),
                    clinicalVol = Convert.ToDouble(reader.GetValue(10)),
                    progMotility = Convert.ToInt32(reader.GetValue(11)),
                    phLevel = Convert.ToDouble(reader.GetValue(12))
                });
            }

            using var cmdAppt = db.CreateCommand();
            cmdAppt.CommandText = "SELECT Timestamp FROM Appointments ORDER BY Timestamp DESC LIMIT 1";
            var apptResult = cmdAppt.ExecuteScalar();
            long appt = apptResult != null ? Convert.ToInt64(apptResult) : 0;
            double min = GetSetting(db, "min_threshold", 24.0);
            double max = GetSetting(db, "max_threshold", 72.0);

            bool isOverdue = false;
            
            long lastReleaseTimestamp = 0;
            string currentVol = "Normal";
            foreach (var l in logs)
            {
                var v = ((dynamic)l).volume;
                if (v != "None" && v != "N/A")
                {
                    lastReleaseTimestamp = ((dynamic)l).timestamp;
                    currentVol = v;
                    break;
                }
            }

            double activeMax = (currentVol == "Low") ? max + 24.0 : max;
            
            if (lastReleaseTimestamp > 0)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                double hoursSinceLast = (now - lastReleaseTimestamp) / 3600.0;
                if (hoursSinceLast > activeMax) isOverdue = true;
            }

            using var checkTestCmd = db.CreateCommand();
            checkTestCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Logs_Backup'";
            bool isTestMode = checkTestCmd.ExecuteScalar() != null;

            var payload = new
            {
                action = "UPDATE_STATE",
                logs = logs,
                appointment = appt,
                settings = new { min = min, max = max, activeMax = activeMax, isOverdue = isOverdue },
                isTestMode = isTestMode
            };

            window.SendWebMessage(JsonSerializer.Serialize(payload));
        }

        static void Generate90DayReport(SqliteConnection db)
        {
            long ninetyDaysAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (90L * 24 * 3600);
            
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT Timestamp, IFNULL(Volume, 'Normal'), IFNULL(HeatFlag, 0), IFNULL(Motility, 0), IFNULL(Concentration, 0), IFNULL(Morphology, 0), IFNULL(Mode, 'Maintenance'), IFNULL(Supplements, '{}'), IFNULL(ClinicalVol, 0.0), IFNULL(ProgMotility, 0), IFNULL(PhLevel, 0.0) FROM Logs WHERE Timestamp >= $ts ORDER BY Timestamp ASC";
            cmd.Parameters.AddWithValue("$ts", ninetyDaysAgo);
            
            using var reader = cmd.ExecuteReader();
            var logs = new List<(long ts, string vol, int heat, int mot, int conc, int morph, string mode, string supplements, double cvol, int pmot, double ph)>();
            while(reader.Read())
            {
                logs.Add((
                    reader.GetInt64(0), 
                    reader.GetString(1), 
                    Convert.ToInt32(reader.GetValue(2)), 
                    Convert.ToInt32(reader.GetValue(3)), 
                    Convert.ToInt32(reader.GetValue(4)), 
                    Convert.ToInt32(reader.GetValue(5)), 
                    reader.GetString(6), 
                    reader.GetString(7), 
                    Convert.ToDouble(reader.GetValue(8)), 
                    Convert.ToInt32(reader.GetValue(9)), 
                    Convert.ToDouble(reader.GetValue(10))
                ));
            }

            var releaseLogs = logs.Where(l => l.vol != "None" && l.vol != "N/A").ToList();
            
            double avgGap = 0;
            double minGap = 999;
            if (releaseLogs.Count > 1)
            {
                double totalGap = 0;
                for (int i = 0; i < releaseLogs.Count - 1; i++)
                {
                    double gap = (releaseLogs[i+1].ts - releaseLogs[i].ts) / 3600.0;
                    totalGap += gap;
                    if (gap < minGap) minGap = gap;
                }
                avgGap = totalGap / (releaseLogs.Count - 1);
            }
            if (minGap == 999) minGap = 0;

            string pdfPath = Path.Combine(dbDir, "Baseline_Summary.pdf");
            
            string pdfFont = Fonts.Arial;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) 
                pdfFont = "Liberation Sans"; 
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) 
                pdfFont = "Helvetica";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(pdfFont));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PIMS BASELINE REPORT").SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);
                            col.Item().Text("Reproductive System Analytics").FontSize(14).FontColor(Colors.Grey.Darken1);
                        });
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text($"Date: {DateTime.Now:MMM dd, yyyy}").SemiBold();
                            col.Item().Text("Cycle: 90-Day Retrospective");
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // 1. STATS HUD ROW
                        col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Total Cycles Recorded").SemiBold().FontColor(Colors.Grey.Darken1);
                                c.Item().Text(logs.Count.ToString()).FontSize(16).SemiBold(); 
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Mean Recovery Gap").SemiBold().FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"{avgGap:F1} Hrs").FontSize(16).SemiBold();
                            });
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Min Recovery Gap").SemiBold().FontColor(Colors.Grey.Darken1);
                                c.Item().Text($"{minGap:F1} Hrs").FontSize(16).SemiBold();
                            });
                        });

                        // 2. VECTOR PDF GRAPHS
                        if (logs.Count > 0)
                        {
                            Action<ColumnDescriptor, string, int, int, string> drawBar = (c, label, value, maxVal, color) => {
                                c.Item().PaddingTop(6).Row(r => {
                                    r.RelativeItem().Text(label).FontSize(9);
                                    r.ConstantItem(30).AlignRight().Text(value.ToString()).FontSize(9).SemiBold();
                                });
                                
                                float pct = maxVal == 0 ? 0 : (float)value / maxVal;
                                float fill = pct * 100f;
                                float empty = 100f - fill;
                                
                                c.Item().PaddingTop(2).Row(r => {
                                    if (fill > 0) r.RelativeItem(fill).Height(8).Background(color);
                                    if (empty > 0) r.RelativeItem(empty).Height(8);
                                });
                            };

                            col.Item().PaddingTop(15).PaddingBottom(15).Row(row =>
                            {
                                // Mode Graph Column
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().PaddingBottom(2).Text("Event Distribution").SemiBold().FontColor(Colors.Grey.Darken2);
                                    
                                    int maintCount = logs.Count(x => x.mode == "Maintenance");
                                    int playCount = logs.Count(x => x.mode == "Playtime");
                                    int babyCount = logs.Count(x => x.mode == "Baby-Making");
                                    int labCount = logs.Count(x => x.mode == "Clinical-Lab");
                                    int maxMode = Math.Max(1, new[] { maintCount, playCount, babyCount, labCount }.Max());

                                    drawBar(c, "Maintenance", maintCount, maxMode, Colors.Blue.Medium);
                                    drawBar(c, "Playtime", playCount, maxMode, Colors.Purple.Medium);
                                    drawBar(c, "Baby-Making", babyCount, maxMode, Colors.Green.Medium);
                                    drawBar(c, "Clinical Lab", labCount, maxMode, Colors.LightBlue.Medium);
                                });

                                row.ConstantItem(40); // Spacer

                                // Yield Graph Column
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().PaddingBottom(2).Text("Yield Profile").SemiBold().FontColor(Colors.Grey.Darken2);
                                    
                                    int highCount = logs.Count(x => x.vol == "High");
                                    int normCount = logs.Count(x => x.vol == "Normal");
                                    int lowCount = logs.Count(x => x.vol == "Low");
                                    int noneCount = logs.Count(x => x.vol == "None" || x.vol == "N/A");
                                    int maxVol = Math.Max(1, new[] { highCount, normCount, lowCount, noneCount }.Max());

                                    drawBar(c, "High Volume", highCount, maxVol, Colors.Green.Medium);
                                    drawBar(c, "Normal Volume", normCount, maxVol, Colors.Blue.Medium);
                                    drawBar(c, "Low Volume", lowCount, maxVol, Colors.Orange.Medium);
                                    drawBar(c, "Dry (None)", noneCount, maxVol, Colors.Grey.Medium);
                                });
                            });
                        }

                        // 3. LOG TABLE
                        col.Item().PaddingTop(10);

                        if (logs.Count == 0)
                        {
                            col.Item().Text("No records found in the last 90 days.").Italic();
                        }
                        else
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);
                                    columns.RelativeColumn(3); 
                                    columns.RelativeColumn(2); 
                                    columns.RelativeColumn(3); 
                                    columns.RelativeColumn(7); 
                                });

                                table.Header(header =>
                                {
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Date").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Mode").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Vol").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Supplements").SemiBold();
                                    header.Cell().BorderBottom(2).BorderColor(Colors.Black).PaddingBottom(5).Text("Lab Results").SemiBold();
                                });

                                bool isAlternate = false;
                                foreach (var log in logs)
                                {
                                    var backgroundColor = isAlternate ? Colors.Grey.Lighten4 : Colors.White;
                                    
                                    if (log.mode == "Clinical-Lab") {
                                        backgroundColor = Colors.Blue.Lighten4;
                                    }

                                    var date = DateTimeOffset.FromUnixTimeSeconds(log.ts).ToLocalTime().ToString("MMM dd HH:mm");
                                    
                                    int zincVal = 0, macaVal = 0, vitDVal = 0, vitCVal = 0;
                                    try 
                                    {
                                        var sDoc = JsonDocument.Parse(log.supplements);
                                        if (sDoc.RootElement.TryGetProperty("zinc", out var zProp)) zincVal = zProp.GetInt32();
                                        if (sDoc.RootElement.TryGetProperty("maca", out var mProp)) macaVal = mProp.GetInt32();
                                        if (sDoc.RootElement.TryGetProperty("vitD", out var dProp)) vitDVal = dProp.GetInt32();
                                        if (sDoc.RootElement.TryGetProperty("vitC", out var cProp)) vitCVal = cProp.GetInt32();
                                    } 
                                    catch {}

                                    List<string> flags = new List<string>();
                                    if (log.heat == 1) flags.Add("Heat");
                                    if (zincVal == 1) flags.Add("Zinc");
                                    if (macaVal == 1) flags.Add("Maca");
                                    if (vitDVal == 1) flags.Add("Vit D3");
                                    if (vitCVal == 1) flags.Add("Vit C");
                                    string flagStr = flags.Count > 0 ? string.Join(", ", flags) : "-";

                                    string labStr = "-";
                                    if (log.conc > 0 || log.mot > 0 || log.morph > 0)
                                    {
                                        labStr = $"Vol: {log.cvol:F1}mL | C: {log.conc}M | Mot: {log.mot}% (P:{log.pmot}%) | Mor: {log.morph}% | pH: {log.ph:F1}";
                                    }

                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(date).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.mode).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(log.vol).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(flagStr).FontSize(9);
                                    table.Cell().Background(backgroundColor).PaddingVertical(5).PaddingHorizontal(2).Text(labStr).FontSize(8).SemiBold();
                                    
                                    isAlternate = !isAlternate;
                                }
                            });
                        }
                    });
                    
                    page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("CONFIDENTIAL MEDICAL RECORD").FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignRight().Text(x => 
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });
            });

            try 
            {
                document.GeneratePdf(pdfPath);
            }
            catch (IOException)
            {
                ShowNotification("File In Use", "Please close the currently open Baseline_Summary.pdf before generating a new one.");
                return;
            }
            catch (Exception ex)
            {
                ShowNotification("PDF Generation Error", ex.Message);
                return;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", pdfPath);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", pdfPath);
            }
            catch (Exception ex)
            {
                ShowNotification("PDF Error", $"Could not open PDF automatically: {ex.Message}");
            }
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
            catch { }
        }
    }
}
