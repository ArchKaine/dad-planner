using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Photino.NET;

namespace WankPlanner
{
    public class LogEntry
    {
        public int id { get; set; }
        public long timestamp { get; set; }
    }

    class Program
    {
        private static string dbPath = "Data Source=inventory.db";
        private static Timer? alertTimer;

        [STAThread]
        static void Main(string[] args)
        {
            InitializeDatabase();

            if (args.Length > 0 && args[0] == "--daemon")
            {
                Console.WriteLine("[+] Starting WankPlanner in background daemon mode...");
                alertTimer = new Timer(CheckStatus, null, 0, 3600000);
                using var cancelEvent = new ManualResetEvent(false);
                cancelEvent.WaitOne();
                return;
            }

            alertTimer = new Timer(CheckStatus, null, 0, 3600000);
            var window = new PhotinoWindow()
                .SetTitle("System Maintenance Utility")
                .SetUseOsDefaultSize(false)
                .SetSize(900, 550)
                .Center()
                .Load("wwwroot/index.html");

            window.RegisterWebMessageReceivedHandler((object? sender, string message) =>
            {
                try
                {
                    if (message == "LOG_EVENT") return; // Catch old caches

                    var payload = JsonDocument.Parse(message).RootElement;
                    string? action = payload.GetProperty("action").GetString();

                    if (action == "LOG_EVENT") { LogEvent(DateTimeOffset.UtcNow.ToUnixTimeSeconds()); SendState(window); }
                    else if (action == "MANUAL_LOG") { LogEvent(payload.GetProperty("timestamp").GetInt64()); SendState(window); }
                    else if (action == "DELETE_LOG") { DeleteLog(payload.GetProperty("id").GetInt32()); SendState(window); }
                    else if (action == "SET_APPOINTMENT") { SetConfig("ohsu_date", payload.GetProperty("timestamp").GetInt64().ToString()); SendState(window); }
                    else if (action == "CLEAR_APPOINTMENT") { SetConfig("ohsu_date", "0"); SendState(window); }
                    else if (action == "EXPORT_CSV") { ExportCSV(window); }
                    else if (action == "GET_STATE") { SendState(window); }
                }
                catch (Exception ex) { Console.WriteLine($"[ERROR] {ex.Message}"); }
            });

            window.WaitForClose();
        }

        static void InitializeDatabase()
        {
            using var connection = new SqliteConnection(dbPath);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS maintenance_log (id INTEGER PRIMARY KEY AUTOINCREMENT, timestamp INTEGER);
                CREATE TABLE IF NOT EXISTS config (key TEXT PRIMARY KEY, value TEXT);
            ";
            command.ExecuteNonQuery();
        }

        static void LogEvent(long unixTime) { ExecuteSql("INSERT INTO maintenance_log (timestamp) VALUES ($val)", unixTime); }
        static void DeleteLog(int id) { ExecuteSql("DELETE FROM maintenance_log WHERE id = $val", id); }
        static void SetConfig(string key, string value) { ExecuteSql("INSERT OR REPLACE INTO config (key, value) VALUES ('ohsu_date', $val)", value); }
        
        static void ExecuteSql(string query, object val)
        {
            using var connection = new SqliteConnection(dbPath);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = query;
            command.Parameters.AddWithValue("$val", val);
            command.ExecuteNonQuery();
        }

        static void ExportCSV(PhotinoWindow window)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WankPlanner_Export.csv");
            using var connection = new SqliteConnection(dbPath);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT timestamp FROM maintenance_log ORDER BY timestamp ASC";
            using var reader = command.ExecuteReader();
            var lines = new List<string> { "EventID,Timestamp,DateTime" };
            int i = 1;
            while (reader.Read())
            {
                long ts = reader.GetInt64(0);
                lines.Add($"{i++},{ts},{DateTimeOffset.FromUnixTimeSeconds(ts).ToString("yyyy-MM-dd HH:mm:ss")}");
            }
            File.WriteAllLines(path, lines);
            
            // Notify UI
            window.Invoke(() => window.SendWebMessage(JsonSerializer.Serialize(new { action = "NOTIFY", message = $"Exported to {path}" })));
        }

        static void SendState(PhotinoWindow window)
        {
            using var connection = new SqliteConnection(dbPath);
            connection.Open();
            
            // Get Logs
            var cmdLogs = connection.CreateCommand();
            cmdLogs.CommandText = "SELECT id, timestamp FROM maintenance_log ORDER BY timestamp DESC LIMIT 50";
            using var reader = cmdLogs.ExecuteReader();
            var logs = new List<LogEntry>();
            while (reader.Read()) logs.Add(new LogEntry { id = reader.GetInt32(0), timestamp = reader.GetInt64(1) });

            // Get Config
            var cmdCfg = connection.CreateCommand();
            cmdCfg.CommandText = "SELECT value FROM config WHERE key = 'ohsu_date'";
            var apptObj = cmdCfg.ExecuteScalar();
            long appt = (apptObj != null && apptObj != DBNull.Value) ? Convert.ToInt64(apptObj) : 0;

            var response = JsonSerializer.Serialize(new { action = "UPDATE_STATE", logs = logs, appointment = appt });
            window.Invoke(() => window.SendWebMessage(response));
        }

        static void CheckStatus(object? state)
        {
            using var connection = new SqliteConnection(dbPath);
            connection.Open();
            
            var cmdAppt = connection.CreateCommand();
            cmdAppt.CommandText = "SELECT value FROM config WHERE key = 'ohsu_date'";
            var apptObj = cmdAppt.ExecuteScalar();
            long targetAppt = (apptObj != null && apptObj != DBNull.Value) ? Convert.ToInt64(apptObj) : 0;
            
            long current = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double hoursToAppt = targetAppt > 0 ? (targetAppt - current) / 3600.0 : 9999;

            // If we are within 120 hours (5 days) of the clinic appointment, suppress normal alerts
            if (hoursToAppt > 0 && hoursToAppt <= 120) return; 

            var cmdLog = connection.CreateCommand();
            cmdLog.CommandText = "SELECT timestamp FROM maintenance_log ORDER BY timestamp DESC LIMIT 1";
            var result = cmdLog.ExecuteScalar();
            
            if (result != null && result != DBNull.Value)
            {
                long lastEvent = Convert.ToInt64(result);
                double deltaHours = (current - lastEvent) / 3600.0;
                if (deltaHours > 72)
                {
                    Process.Start(new ProcessStartInfo {
                        FileName = "notify-send",
                        Arguments = "-u normal -a \"System Monitor\" \"System Task\" \"Routine Personal Maintenance Required.\"",
                        RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                    });
                }
            }
        }
    }
}
