using JocysCom.VS.DemoProjects.Project1;
using System;

namespace JocysCom.VS.AiCompanion.Demo
{
	public class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			// Test warning: Unused variable.
			int unusedVariable;

			// Test warning: Obsolete
			var value = Class2.Method1();
			Console.Write(value);

			// Test exception.
			var z = Class1.Divide(10, 0);
		}

	}
}
