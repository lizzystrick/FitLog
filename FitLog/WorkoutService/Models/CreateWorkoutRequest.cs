using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WorkoutService.Models
{
    public class CreateWorkoutRequest
    {
        public DateTime Date { get; set; }
        public int DurationMinutes { get; set; }
    }
}
