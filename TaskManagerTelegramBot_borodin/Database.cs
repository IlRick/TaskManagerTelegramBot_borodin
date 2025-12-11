using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskManagerTelegramBot_borodin.Classes;

namespace TaskManagerTelegramBot_borodin
{
    public class Database
    {
        string connection = "server=localhost;user=root;password=;database=taskmanagerbot;";

        public void AddUser(long chatId, string username)
        {
            using var conn = new MySqlConnection(connection);
            conn.Open();
            string q = "INSERT IGNORE INTO users (id, username) VALUES (@i, @u)";
            using var cmd = new MySqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@i", chatId);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.ExecuteNonQuery();
        }
        public void AddEvent(long userId, string message,DateTime? eventTime, string recurrenceType,   string weeklyDays,        TimeSpan? timeOfDay)      
        {
            using var conn = new MySqlConnection(connection);
            conn.Open();
            DateTime nextRun;
            if (recurrenceType == "none")
            {
                if (!eventTime.HasValue) throw new ArgumentException("eventTime required for non-recurring event");
                nextRun = eventTime.Value;
            }
            else if (recurrenceType == "daily")
            {
                if (!timeOfDay.HasValue) throw new ArgumentException("timeOfDay required for daily");
                var today = DateTime.Today.Add(timeOfDay.Value);
                nextRun = today >= DateTime.Now ? today : today.AddDays(1);
            }
            else if (recurrenceType == "weekly")
            {
                if (!timeOfDay.HasValue || string.IsNullOrEmpty(weeklyDays)) throw new ArgumentException("weeklyDays and timeOfDay required for weekly");
                var days = weeklyDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                List<DayOfWeek> target = new List<DayOfWeek>();
                foreach (var d in days)
                {
                    if (Enum.TryParse<DayOfWeek>(d, true, out var dd)) target.Add(dd);
                }
                DateTime candidate = DateTime.MinValue;
                for (int i = 0; i < 7; i++)
                {
                    var check = DateTime.Today.AddDays(i).Date + timeOfDay.Value;
                    if (target.Contains(check.DayOfWeek))
                    {
                        if (check >= DateTime.Now) { candidate = check; break; }
                    }
                }
                if (candidate == DateTime.MinValue)
                {
                    DateTime best = DateTime.MaxValue;
                    foreach (var d in target)
                    {
                        int daysTo = ((int)d - (int)DateTime.Today.DayOfWeek + 7) % 7;
                        if (daysTo == 0) daysTo = 7;
                        var dt = DateTime.Today.AddDays(daysTo).Date + timeOfDay.Value;
                        if (dt < best) best = dt;
                    }
                    candidate = best;
                }
                nextRun = candidate;
            }
            else throw new ArgumentException("Unknown recurrenceType");

            string q = @"INSERT INTO events (user_id, message, recurrence_type, weekly_days, time_of_day, event_time, next_run)
                         VALUES (@u, @m, @r, @wd, @tod, @et, @nr)";
            using var cmd = new MySqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@m", message);
            cmd.Parameters.AddWithValue("@r", recurrenceType);
            cmd.Parameters.AddWithValue("@wd", (object)weeklyDays ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tod", timeOfDay.HasValue ? (object)timeOfDay.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@et", eventTime.HasValue ? (object)eventTime.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@nr", nextRun);
            cmd.ExecuteNonQuery();
        }
        public List<Events> GetDueEvents()
        {
            var list = new List<Events>();
            using var conn = new MySqlConnection(connection);
            conn.Open();
            string q = "SELECT id, user_id, event_time, next_run, time_of_day, message, recurrence_type, weekly_days FROM events WHERE next_run <= @now";
            using var cmd = new MySqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@now", DateTime.Now);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                long userId = reader.GetInt64(1);
                DateTime? eventTime = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                DateTime nextRun = reader.GetDateTime(3);
                TimeSpan? timeOfDay = reader.IsDBNull(4) ? (TimeSpan?)null : reader.GetTimeSpan(4);
                string message = reader.GetString(5);
                string recurrenceType = reader.GetString(6);
                string weeklyDays = reader.IsDBNull(7) ? null : reader.GetString(7);
                var ev = new Events(
     id,
     userId,
     eventTime,
     nextRun,
     timeOfDay,
     message,
     recurrenceType,
     weeklyDays
 );
                list.Add(ev);
            }
            return list;
        }
        public void UpdateNextRun(long eventId, DateTime nextRun)
        {
            using var conn = new MySqlConnection(connection);
            conn.Open();
            string q = "UPDATE events SET next_run=@nr WHERE id=@id";
            using var cmd = new MySqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@nr", nextRun);
            cmd.Parameters.AddWithValue("@id", eventId);
            cmd.ExecuteNonQuery();
        }
        public void DeleteEventById(long eventId)
        {
            using var conn = new MySqlConnection(connection);
            conn.Open();
            string q = "DELETE FROM events WHERE id=@id";
            using var cmd = new MySqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@id", eventId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteAllEvents(long userId)
        {
            using var conn = new MySqlConnection(connection);
            conn.Open();
            string q = "DELETE FROM events WHERE user_id=@u";
            using var cmd = new MySqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.ExecuteNonQuery();
        }

        public List<Events> GetUserEvents(long userId)
        {
            var list = new List<Events>();

            using var conn = new MySqlConnection(connection);
            conn.Open();

            string q = "SELECT id, user_id, event_time, next_run, time_of_day, message, recurrence_type, weekly_days FROM events WHERE user_id=@u";
            using var cmd = new MySqlCommand(q, conn);
            cmd.Parameters.AddWithValue("@u", userId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                long uid = reader.GetInt64(1);
                DateTime? eventTime = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                DateTime nextRun = reader.GetDateTime(3);
                TimeSpan? timeOfDay = reader.IsDBNull(4) ? null : reader.GetTimeSpan(4);
                string message = reader.GetString(5);
                string recurrenceType = reader.GetString(6);
                string weeklyDays = reader.IsDBNull(7) ? null : reader.GetString(7);

                list.Add(new Events(id, uid, eventTime, nextRun, timeOfDay, message, recurrenceType, weeklyDays));
            }

            return list;
        }
    }

}
