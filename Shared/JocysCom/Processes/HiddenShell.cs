using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace JocysCom.ClassLibrary.Processes
{
	/// <summary>
	/// Summary description for HiddenShell.
	/// </summary>
	public class HiddenShell : IDisposable
	{

		public Process CmdProcess;
		private readonly ProcessStartInfo ProcessInfo;

		private System.IO.StreamWriter sw;
		private System.IO.StreamReader sr;
		private System.IO.StreamReader err;
		private bool alreadyStarted;

		public StringBuilder Log { get; }

		public HiddenShell()
		{
			CmdProcess = new Process();
			Log = new StringBuilder();
			ProcessInfo = new ProcessStartInfo("cmd");
			ProcessInfo.UseShellExecute = false;
			Start(false);
		}

		public void Start(bool show)
		{
			if (!show && !alreadyStarted)
			{
				ProcessInfo.RedirectStandardInput = true;
				ProcessInfo.RedirectStandardOutput = true;
				ProcessInfo.RedirectStandardError = true;
				// Hide CMD window.
				//psI.WindowStyle = ProcessWindowStyle.Hidden;
				ProcessInfo.CreateNoWindow = true;
				CmdProcess.StartInfo = ProcessInfo;
				CmdProcess.Start();
				if (!show)
				{
					// Redirect input/output.
					sw = CmdProcess.StandardInput;
					sr = CmdProcess.StandardOutput;
					err = CmdProcess.StandardError;
				}
				alreadyStarted = true;
			}
			// Other options.
			sw.AutoFlush = true;
			// Wait for exit if window is visible.
			if (show)
				CmdProcess.WaitForExit();
		}

		public void Stop()
		{
			// Kill process if not exited.
			if (!CmdProcess.HasExited)
				CmdProcess.Kill();
			sw?.Close();
			sr?.Close();
			err?.Close();
		}


		// Return all command line text.
		public string ExecuteCommand(string command)
		{
			if (string.IsNullOrEmpty(command))
				throw new ArgumentNullException(nameof(command));
			sw.WriteLine(command);
			//sw.Flush();
			sw.Close();
			var results = sr.ReadToEnd();
			var errorResults = err.ReadToEnd();
			Log.Append(results);
			Log.Append(errorResults);
			return results + errorResults;
		}

		// Function to run once and return only output of command.
		public static string Execute(string command)
		{
			if (string.IsNullOrEmpty(command))
				throw new ArgumentNullException(nameof(command));
			var outBuilder = new StringBuilder();
			Execute("cmd.exe", "/c " + command, outBuilder);
			return outBuilder.ToString();
		}

		/// <summary>
		/// Execute console command.
		/// </summary>
		/// <param name="fileName">Console application to execute.</param>
		/// <param name="arguments">Arguments.</param>
		/// <param name="outBuilder">String builder for output.</param>
		/// <param name="errBuilder">String builder for errors. If null then output will be used.</param>
		/// <param name="timeout">Timeout. Default is -1, which represents an infinite time-out.</param>
		/// <returns>Exit code. Return null if timeout.</returns>
		public static int? Execute(string fileName, string arguments, StringBuilder outBuilder, StringBuilder errBuilder = null, int timeout = -1)
		{
			int? exitCode = null;
			var si = new ProcessStartInfo();
			// Set correct encoding.
			//si.StandardErrorEncoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
			//si.StandardOutputEncoding = Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
			// Do not use the OS shell.
			si.UseShellExecute = false;
			// Allow writing output to the standard output.
			si.RedirectStandardOutput = true;
			// Allow writing error to the standard error.
			si.RedirectStandardError = true;
			// Hide window.
			si.CreateNoWindow = true;
			si.FileName = fileName;
			si.Arguments = arguments;
			if (errBuilder is null)
				errBuilder = outBuilder;
			using (var outputWaitHandle = new AutoResetEvent(false))
			{
				using (var errorsWaitHandle = new AutoResetEvent(false))
				{
					using (var p = new Process() { StartInfo = si })
					{
						var receiveLock = new object();
						//var receiveLine = 0;
						var receiveSkip = false;
						var action = new Action<AutoResetEvent, DataReceivedEventArgs, StringBuilder>((ev, e, sb) =>
						{
							// If redirected stream is closed (a null line is sent) then...
							if (e.Data is null)
								// Allow WaitOne line to proceed.
								ev.Set();
							// If double empty line then skip...
							else if (e.Data.Length == 0 && receiveSkip) { receiveSkip = false; }
							else
								lock (receiveLock)
								{
									//sb.AppendFormat("{0}. {1}\r\n", ++receiveLine, e.Data);
									sb.AppendLine(e.Data);
									// Workaround: Allow to skip next empty line.
									receiveSkip = true;
								}
						});
						// Inside event call function with correct handler and string builder.
						var outputReceived = new DataReceivedEventHandler((sender, e) => action(outputWaitHandle, e, outBuilder));
						var errorsReceived = new DataReceivedEventHandler((sender, e) => action(errorsWaitHandle, e, errBuilder));
						p.OutputDataReceived += outputReceived;
						p.ErrorDataReceived += errorsReceived;
						p.Start();
						p.BeginErrorReadLine();
						p.BeginOutputReadLine();
						if (p.WaitForExit(timeout))
							// Process completed. Check process.ExitCode here.
							exitCode = p.ExitCode;
						p.Close();
						// Detach events before disposing process.
						// If timeout is set and is too small then events will be detached before all data received.
						p.OutputDataReceived -= outputReceived;
						p.ErrorDataReceived -= errorsReceived;
					}
					errorsWaitHandle.WaitOne(timeout);
				}
				// Timeout handlers after 'p' is disposed to make sure that handers are not used in events.
				outputWaitHandle.WaitOne(timeout);
			}
			return exitCode;
		}

		#region IDisposable

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		// The bulk of the clean-up code is implemented in Dispose(bool)
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (CmdProcess != null)
				{
					CmdProcess.Dispose();
					CmdProcess = null;
				}
				sw?.Dispose();
				sr?.Dispose();
				err?.Dispose();
			}
		}

		#endregion

	}
}
