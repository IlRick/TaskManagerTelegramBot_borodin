using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskManagerTelegramBot_borodin.Classes
{
    public class Events
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public DateTime? EventTime { get; set; } 
        public DateTime NextRun { get; set; }  
        public TimeSpan? TimeOfDay { get; set; }  
        public string Message { get; set; }
        public string RecurrenceType { get; set; } 
        public string WeeklyDays { get; set; }   
        public Events(long id, long userId, DateTime? eventTime, DateTime nextRun, TimeSpan? timeOfDay, string message, string recurrenceType, string weeklyDays)
        {
            Id = id;
            UserId= userId;
            EventTime = eventTime;
            NextRun = nextRun;
            TimeOfDay = timeOfDay;
            Message = message;
            RecurrenceType = recurrenceType;
            WeeklyDays = weeklyDays;
        }
    }

}
