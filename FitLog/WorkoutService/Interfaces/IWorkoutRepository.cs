using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorkoutService.Models;

namespace WorkoutService.Interfaces
{
    interface IWorkoutRepository
    {
        void Add(Workout workout);
        IEnumerable<Workout> GetByUser(string userId);
    }
}
