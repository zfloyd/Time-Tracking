using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeTracking
{
	public class Group
	{
		[Key]
		public int GroupID { get; set; }
		[Required, StringLength(255)]
		public string Name { get; set; }

		public virtual ICollection<IdleEvent> IdleEvents { get; set; }
		public virtual ICollection<Program> Programs { get; set; }
	}
	public class IdleEvent
	{
		[Key]
		public int IdleEventID { get; set; }
		public int? GroupID { get; set; }
		[Required, StringLength(255)]
		public string Name { get; set; }
		[Required]
		public bool DisplayInList { get; set; }

		[ForeignKey("GroupID")]
		public virtual Group Group { get; set; }
		public virtual ICollection<Activity> Activities { get; set; }
	}

	public class Program
	{
		[Key]
		public int ProgramID { get; set; }
		public int? GroupID { get; set; }
		[Required, StringLength(255)]
		public string Name { get; set; }
		[StringLength(255)]
		public string ProcessName { get; set; }

		[ForeignKey("GroupID")]
		public virtual Group Group { get; set; }
		public virtual ICollection<Activity> Activities { get; set; }
		public virtual ICollection<ProgramWindow> ProgramWindows { get; set; }
	}

	public class ProgramWindow
	{
		[Key]
		public int ProgramWindowID { get; set; }
		[Required]
		public int ProgramID { get; set; }
		[Required, StringLength(255)]
		public string Title { get; set; }

		[ForeignKey("ProgramID")]
		public virtual Program Program { get; set; }
		public virtual ICollection<Activity> Activities { get; set; }
	}

	public class Activity
	{
		[Key]
		public int ActivityID { get; set; }
		public int? IdleEventID { get; set; }
		public int? ProgramID { get; set; }
		public int? ProgramWindowID { get; set; }
		[Required]
		public DateTime Started { get; set; }
		public DateTime? Ended { get; set; }

		[ForeignKey("IdleEventID")]
		public virtual IdleEvent IdleEvent { get; set; }
		[ForeignKey("ProgramID")]
		public virtual Program Program { get; set; }
		[ForeignKey("ProgramWindowID")]
		public virtual ProgramWindow ProgramWindow { get; set; }
	}
}
