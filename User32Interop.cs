using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TimeTracking
{
	public static class User32Interop
	{
		public static Process GetCurrentProcess()
		{
			return GetProcessByHandle(GetForegroundWindow()); ;
		}
		private static Process GetProcessByHandle(IntPtr hwnd)
		{
			try
			{
				uint processID;
				GetWindowThreadProcessId(hwnd, out processID);
				return Process.GetProcessById((int)processID);
			}
			catch { return null; }
		}

		public static TimeSpan GetLastInput()
		{
			var plii = new LASTINPUTINFO();
			plii.cbSize = (uint)Marshal.SizeOf(plii);

			if (GetLastInputInfo(ref plii))
				return TimeSpan.FromMilliseconds(Environment.TickCount - plii.dwTime);
			else
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		private static extern Int32 GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

		struct LASTINPUTINFO
		{
			public uint cbSize;
			public uint dwTime;
		}
	}
}
