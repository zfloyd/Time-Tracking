using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeTracking
{
	public class TrackingContext :DbContext
	{
		public DbSet<Activity> Activities { get; set; }
		public DbSet<IdleEvent> IdleEvents { get; set; }
		public DbSet<Program> Programs {get;set;}
		public DbSet<ProgramWindow> ProgramWindows { get; set; }
	}
}
