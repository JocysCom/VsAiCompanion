using System;

namespace JocysCom.VS.DemoProjects.Project1
{
	public class Class2
	{
		[Obsolete("Method1 is deprecated. Please use `Method2()` instead.")]
		public static bool Method1()
		{
			return false;
		}

		public static bool Method2()
		{
			return true;
		}

	}
}
