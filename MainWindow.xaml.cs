using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace TimeTracking
{
	public partial class MainWindow : Window
	{
		private static Activity m_CurrentActivity;

		private static Activity m_LastProgramActivity;

		private static string m_CurrentProcess = string.Empty;

		private static TimeSpan m_ActivityThreshold = TimeSpan.FromSeconds(60);

		private static TrackingContext context = new TrackingContext();

		private static DateTime? m_LastActivityTime = null;

		private static IntPtr m_TrackingApplicationID = Process.GetCurrentProcess().MainWindowHandle;

		public MainWindow()
		{
			InitializeComponent();

			System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();
			notifyIcon.Icon = new System.Drawing.Icon("clock.ico");
			System.Windows.Forms.ContextMenu menu = new System.Windows.Forms.ContextMenu();
			menu.MenuItems.Add(new System.Windows.Forms.MenuItem("Today's Activity", TodaysActivity_Click));
			menu.MenuItems.Add(new System.Windows.Forms.MenuItem("Today's Browsing Activity", TodaysBrowsing_Click));
			menu.MenuItems.Add(new System.Windows.Forms.MenuItem("Yesterday's Activity", YesterdaysActivity_Click));
			menu.MenuItems.Add(new System.Windows.Forms.MenuItem("Edit Program Groups", ProgramGroups_Click));
			menu.MenuItems.Add(new System.Windows.Forms.MenuItem("Edit Idle Event Groups", IdleGroups_Click));
			menu.MenuItems.Add(new System.Windows.Forms.MenuItem("E&xit", Exit_Click));
			notifyIcon.ContextMenu = menu;
			notifyIcon.Visible = true;

			//GetTodaysActivity();
			DispatcherTimer timer = new DispatcherTimer();
			timer.Tick += timer_Tick;
			timer.Interval = new TimeSpan(0, 0, 1);
			timer.Start();

			System.Windows.Application.Current.Exit += Current_Exit;
		}

		void timer_Tick(object sender, EventArgs e)
		{
			try
			{
				if (IdleWindow.Visibility == System.Windows.Visibility.Visible) //Don't do anything if tracking popup is up
					return;

				if (!m_LastActivityTime.HasValue && User32Interop.GetLastInput() > m_ActivityThreshold) //Just went idle
					m_LastActivityTime = DateTime.Now.AddSeconds(-(User32Interop.GetLastInput().TotalSeconds));
				else if (m_LastActivityTime.HasValue && User32Interop.GetLastInput() <= m_ActivityThreshold) //Just came back
					ShowIdleEventPopup();
				else //Is currently active
				{
					Process currentProcessInfo = User32Interop.GetCurrentProcess();

					if (m_TrackingApplicationID == currentProcessInfo.MainWindowHandle) //Ignore the tracking app
						return;
					string newProcess = currentProcessInfo.MainWindowTitle;
					if (String.IsNullOrEmpty(newProcess))
						newProcess = currentProcessInfo.ProcessName;
					if (!String.IsNullOrEmpty(newProcess))
					{
						if (m_CurrentProcess != newProcess)
						{
							if (m_CurrentActivity != null)
							{
								m_CurrentActivity.Ended = DateTime.Now;
								context.SaveChanges();
							}

							string processName = currentProcessInfo.ProcessName;
							string programName = newProcess.Split('-')[newProcess.Split('-').Length - 1].Trim();
							Program currentProgram = context.Programs.FirstOrDefault(s => s.ProcessName == processName);
							if (currentProgram == null)
							{
								currentProgram = context.Programs.FirstOrDefault(s => s.Name == programName);
								if (currentProgram != null)
									currentProgram.ProcessName = processName;
							}

							if (currentProgram == null)
							{
								currentProgram = new Program();
								currentProgram.Name = programName;
								currentProgram.ProcessName = processName;
								context.Programs.Add(currentProgram);
								context.SaveChanges();
							}

							ProgramWindow currentProgramWindow = null;
							if (newProcess.Contains("-"))
							{
								string windowTitle = newProcess.Substring(0, newProcess.IndexOf(programName)).Trim().TrimEnd('-').Trim();
								currentProgramWindow = context.ProgramWindows.FirstOrDefault(s => s.Title == windowTitle);
								if (currentProgramWindow == null)
								{
									currentProgramWindow = new ProgramWindow();
									currentProgramWindow.Title = windowTitle;
									currentProgramWindow.ProgramID = currentProgram.ProgramID;
									context.ProgramWindows.Add(currentProgramWindow);
									context.SaveChanges();
								}
							}

							m_CurrentActivity = new Activity();
							m_CurrentActivity.ProgramID = currentProgram.ProgramID;
							if (currentProgramWindow != null)
								m_CurrentActivity.ProgramWindowID = currentProgramWindow.ProgramWindowID;
							m_CurrentActivity.Started = DateTime.Now;

							context.Activities.Add(m_CurrentActivity);
							context.SaveChanges();

							m_LastProgramActivity = m_CurrentActivity;
						}
					}

					m_CurrentProcess = newProcess;
				}
			}
			catch (Exception ex)
			{

			}
		}

		void ShowIdleEventPopup()
		{
			uxIdleEvents.ItemsSource = context.IdleEvents.Where(s => s.DisplayInList).OrderBy(s => s.Name).ToList();
			IdleWindow.Visibility = System.Windows.Visibility.Visible;
		}

		protected void uxOldEventSubmit_Click(object sender, RoutedEventArgs e)
		{
			int idleEventID = Convert.ToInt32(((Button)sender).Tag);
			CommonIdleEvent(idleEventID);
		}

		protected void uxNewEventSubmit_Click(object sender, RoutedEventArgs e)
		{
			IdleEvent idleEvent = new IdleEvent();
			idleEvent.Name = uxNewEventName.Text;
			idleEvent.DisplayInList = true;

			context.IdleEvents.Add(idleEvent);
			context.SaveChanges();

			CommonIdleEvent(idleEvent.IdleEventID);

			uxNewEventName.Text = string.Empty;
		}

		protected void uxActiveWindowSubmit_Click(object sender, RoutedEventArgs e)
		{
			CommonIdleEvent(null);
		}

		void CommonIdleEvent(int? idleEventID)
		{
			IdleWindow.Visibility = System.Windows.Visibility.Hidden;
			if (!idleEventID.HasValue)
			{
				m_LastActivityTime = null;
				return;
			}

			try
			{
				if (m_CurrentActivity != null)
				{
					m_CurrentActivity.Ended = m_LastActivityTime;
					context.SaveChanges();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("ActivityID: " + m_CurrentActivity.ActivityID + Environment.NewLine + ex.Message + " : " + Environment.NewLine + ex.StackTrace);
			}

			m_CurrentActivity = new Activity();
			m_CurrentActivity.IdleEventID = idleEventID;
			m_CurrentActivity.Started = m_LastActivityTime.Value;
			m_CurrentActivity.Ended = DateTime.Now;

			context.Activities.Add(m_CurrentActivity);
			context.SaveChanges();

			m_LastActivityTime = null;
			m_CurrentActivity = null;
			m_CurrentProcess = "";
		}

		void GetTodaysActivity()
		{
			GetActivity(DateTime.Now.Date, DateTime.Now);
		}

		void GetActivity(DateTime start, DateTime end)
		{
			try
			{
				List<Activity> activities = context.Activities.Include("Program").Include("Program.Group").Include("ProgramWindow").Include("IdleEvent").Include("IdleEvent.Group").Where(s => s.Started >= start && s.Started <= end && s.Ended.HasValue).ToList();
				List<Activity> computerActivities = activities.Where(s => s.ProgramID.HasValue).ToList();
				List<Activity> idleActivities = activities.Where(s => s.IdleEventID.HasValue).ToList();

				Dictionary<string, double> activityTime = computerActivities.GroupBy(s => (s.Program.GroupID.HasValue ? s.Program.Group.Name : s.Program.Name)).ToDictionary(s => s.Key, s => s.Sum(a => (a.Ended.Value - a.Started).TotalSeconds));

				double totalTimeSpent = activities.Sum(s => (s.Ended.Value - s.Started).TotalSeconds);
				double computerTime = computerActivities.Sum(s => (s.Ended.Value - s.Started).TotalSeconds);
				double idleTime = idleActivities.Sum(s => (s.Ended.Value - s.Started).TotalSeconds);

				string report = "Computer Activities - " + (computerTime / totalTimeSpent).ToString("P") + Environment.NewLine;
				report += string.Join(Environment.NewLine, activityTime.OrderByDescending(s => s.Value).Select(s => s.Key + ": " + FormatSeconds(s.Value)).ToList());

				activityTime = idleActivities.GroupBy(s => (s.IdleEvent.GroupID.HasValue ? s.IdleEvent.Group.Name : s.IdleEvent.Name)).ToDictionary(s => s.Key, s => s.Sum(a => (a.Ended.Value - a.Started).TotalSeconds));
				report += Environment.NewLine + Environment.NewLine + "Idle Activities - " + (idleTime / totalTimeSpent).ToString("P") + Environment.NewLine;
				report += string.Join(Environment.NewLine, activityTime.OrderByDescending(s => s.Value).Select(s => s.Key + ": " + FormatSeconds(s.Value)).ToList());

				MessageBox.Show(report);
			}
			catch (Exception ex)
			{
			}
		}

		void GetBrowsingActivity(DateTime start, DateTime end)
		{
			try
			{
				List<Activity> activities = context.Activities.Include("ProgramWindow").Where(s => s.ProgramWindowID.HasValue && s.Started >= start && s.Started <= end && s.Ended.HasValue).ToList();

				Dictionary<string, double> activityTime = activities.GroupBy(s => s.ProgramWindow.Title).ToDictionary(s => s.Key, s => s.Sum(a => (a.Ended.Value - a.Started).TotalSeconds));

				double totalTimeSpent = activities.Sum(s => (s.Ended.Value - s.Started).TotalSeconds);

				string report = "Browsing Activity - " + FormatSeconds(totalTimeSpent) + Environment.NewLine;
				report += string.Join(Environment.NewLine, activityTime.OrderByDescending(s => s.Value).Select(s => s.Key + ": " + FormatSeconds(s.Value)).ToList());

				MessageBox.Show(report);
			}
			catch (Exception ex)
			{
			}
		}

		private string FormatSeconds(double seconds)
		{
			TimeSpan span = new TimeSpan(0, 0, (int)seconds);
			string formatted = string.Format("{0}{1}{2}",
			span.Duration().Hours > 0 ? string.Format("{0:0}h ", span.Hours) : string.Empty,
			span.Duration().Minutes > 0 ? string.Format("{0:0}m ", span.Minutes) : string.Empty,
			span.Duration().Seconds > 0 ? string.Format("{0:0}s ", span.Seconds) : string.Empty);

			if (string.IsNullOrEmpty(formatted)) formatted = "N/A";

			return formatted.Trim();
		}

		void Current_Exit(object sender, ExitEventArgs e)
		{
			if (m_CurrentActivity != null)
			{
				m_CurrentActivity.Ended = DateTime.Now;
				context.SaveChanges();
			}
		}

		private static bool m_EditingProgramGroups = false;

		private void ProgramGroups_Click(object Sender, EventArgs e)
		{
			IdleWindow.Visibility = System.Windows.Visibility.Visible;
			uxGroupPH.Visibility = System.Windows.Visibility.Visible;
			uxIdlePH.Visibility = System.Windows.Visibility.Collapsed;

			uxGroups.ItemsSource = context.Programs.Select(s => new ProgramGroupKVP { ProgramName = s.Name, GroupName = s.GroupID.HasValue ? s.Group.Name : "" }).OrderBy(s => s.GroupName).ToList();
			m_EditingProgramGroups = true;
		}

		private void IdleGroups_Click(object Sender, EventArgs e)
		{
			IdleWindow.Visibility = System.Windows.Visibility.Visible;
			uxGroupPH.Visibility = System.Windows.Visibility.Visible;
			uxIdlePH.Visibility = System.Windows.Visibility.Collapsed;

			uxGroups.ItemsSource = context.IdleEvents.Select(s => new ProgramGroupKVP { ProgramName = s.Name, GroupName = s.GroupID.HasValue ? s.Group.Name : "" }).OrderBy(s => s.GroupName).ToList();
			m_EditingProgramGroups = false;
		}

		public class ProgramGroupKVP
		{
			public string ProgramName { get; set; }
			public string GroupName { get; set; }
		}

		protected void uxGroupSave_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				foreach (ProgramGroupKVP item in uxGroups.Items)
				{
					Group groupEntity = null;
					if (!String.IsNullOrEmpty(item.GroupName))
					{
						groupEntity = context.Groups.FirstOrDefault(s => s.Name == item.GroupName);
						if (groupEntity == null)
						{
							groupEntity = new Group { Name = item.GroupName };
							context.Groups.Add(groupEntity);
							context.SaveChanges();
						}

						if (m_EditingProgramGroups)
						{
							context.Programs.Where(s => s.Name == item.ProgramName).ToList().ForEach(s =>
							{
								s.GroupID = groupEntity == null ? null : (int?)groupEntity.GroupID;
							});
						}
						else
						{
							context.IdleEvents.Where(s => s.Name == item.ProgramName).ToList().ForEach(s =>
							{
								s.GroupID = groupEntity == null ? null : (int?)groupEntity.GroupID;
							});
						}
					}
				}
				context.SaveChanges();
			}
			catch (Exception ex)
			{
			}
			IdleWindow.Visibility = System.Windows.Visibility.Hidden;
			uxGroupPH.Visibility = System.Windows.Visibility.Collapsed;
			uxIdlePH.Visibility = System.Windows.Visibility.Visible;
		}

		private void TodaysActivity_Click(object Sender, EventArgs e)
		{
			GetTodaysActivity();
		}

		private void YesterdaysActivity_Click(object Sender, EventArgs e)
		{
			GetActivity(DateTime.Now.Date.AddDays(-1), DateTime.Now.Date);
		}

		private void TodaysBrowsing_Click(object Sender, EventArgs e)
		{
			GetBrowsingActivity(DateTime.Now.Date, DateTime.Now);
		}

		private void Exit_Click(object Sender, EventArgs e)
		{
			// Close the form, which closes the application. 
			this.Close();
		}
	}
}