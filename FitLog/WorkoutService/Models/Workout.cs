using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WorkoutService.Models
{
    public class Workout
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = "";
        public DateTime Date { get; set; }
        public int DurationMinutes { get; set; }
    }
}
