using System.Windows;

namespace JocysCom.VS.AiCompanion.Resources
{
	partial class Icons_Default : ResourceDictionary
	{
		public Icons_Default()
		{
			InitializeComponent();
		}

		public static Icons_Default Current => _Current = _Current ?? new Icons_Default();
		private static Icons_Default _Current;

		public const string Icon_AiCompanion = nameof(Icon_AiCompanion);

	}
}
