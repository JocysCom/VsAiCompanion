using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Windows.Controls;
using System.Linq;

namespace JocysCom.ClassLibrary.Controls.DynamicCompile
{
	/// <summary>
	/// Interaction logic for CompilerControl.xaml
	/// </summary>
	public partial class CompilerControl : UserControl
	{
		public CompilerControl()
		{
			RunTimer = new System.Windows.Forms.Timer();
			RunTimer.Interval = 1000;
			RunTimer.Tick += RunTimer_Tick;
			InitializeComponent();
		}

		System.Windows.Forms.Timer RunTimer;
		System.Windows.Forms.OpenFileDialog OpenCodeFileDialog = new System.Windows.Forms.OpenFileDialog();
		FileSystemWatcher CodeFileSystemWatcher = new FileSystemWatcher();

		public void LoadScript(LanguageType language, string contents)
		{
			UpdateStats();
			LanguageComboBox.Text = language.ToString();
			CodeTextBox.Text = contents;
		}

		public void LoadFile(string fileName)
		{
			string contents;
			try
			{
				contents = File.ReadAllText(fileName);
			}
			catch (Exception) { return; }
			var extension = Path.GetExtension(fileName);
			var ln = LanguageType.CSharp;
			switch (extension)
			{
				case ".cs":
					ln = LanguageType.CSharp;
					break;
				case ".vb":
					ln = LanguageType.VB;
					break;
				case ".js":
					ln = LanguageType.JScript;
					break;
				default:
					throw new NotSupportedException(extension);
			}
			if (AutoLoadCheckBox.IsChecked == true)
			{
				var fi = new FileInfo(fileName);
				CodeFileSystemWatcher.Path = fi.DirectoryName;
				CodeFileSystemWatcher.Filter = fi.Name;
				CodeFileSystemWatcher.EnableRaisingEvents = true;
			}
			else
			{
				CodeFileSystemWatcher.EnableRaisingEvents = false;
			}
			LoadScript(ln, contents);
		}

		private LanguageType CurrentLanguage => (LanguageType)Enum.Parse(typeof(LanguageType), (string)LanguageComboBox.SelectedItem);

		public void Clear()
		{
			CodeTextBox.Text = string.Empty;
			ClearOutput();
		}

		public void ClearOutput()
		{
			ErrorsTextBox.Text = string.Empty;
			OutputTextBox.Text = string.Empty;
			OutputDataGridView.ItemsSource = null;
		}

		public bool Check()
		{
			ClearOutput();
			var code = CodeTextBox.Text.Trim();
			var selected = EntryComboBox.Text;
			var engine = new DcEngine(code, CurrentLanguage, string.Empty);
			var entryPoints = engine.GetEntryPoints(true);
			if (engine.ErrorsLog.Length > 0)
			{
				ErrorsTextBox.AppendText(engine.ErrorsLog.ToString());
			}
			EntryComboBox.Items.Clear();
			foreach (var entryPoint in entryPoints)
				EntryComboBox.Items.Add(entryPoint);
			if (EntryComboBox.Items.Contains(selected))
			{
				EntryComboBox.Text = selected;
			}
			else if (EntryComboBox.Items.Count == 1)
			{
				EntryComboBox.Text = EntryComboBox.Items.Cast<string>().First();
			}
			if (AutoRunCheckBox.IsChecked == true && EntryComboBox.Text.Length > 0)
				Run();
			return true;
		}

		public void Run(params object[] parameters)
		{
			var code = CodeTextBox.Text.Trim();
			var entry = EntryComboBox.Text;
			if (code == "")
				return;
			if (entry == "")
				entry = "Main";
			var engine = new DcEngine(code, CurrentLanguage, entry);
			var results = engine.Run(parameters);
			if (results is System.Collections.IEnumerable enumerable){
				OutputDataGridView.ItemsSource = enumerable;
				OutputDataGridView.Visibility = System.Windows.Visibility.Visible;
				OutputTextBox.Visibility = System.Windows.Visibility.Collapsed;
			}
			else
			{
				OutputTextBox.Text = results == null 
					? "null"
					: results is string
						? (string)results
						: results.GetType().FullName;
				OutputTextBox.Visibility = System.Windows.Visibility.Visible;
				OutputDataGridView.Visibility = System.Windows.Visibility.Collapsed;
			}
			if (engine.ErrorsLog.Length > 0)
			{
				ErrorsTextBox.Text = engine.ErrorsLog.ToString();
			}
		}

		private int changed = 0;
		private int loaded = 0;

		private void CodeFileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			changed++;
			UpdateStats();
			if (e.ChangeType == WatcherChangeTypes.Changed)
			{
				// Stop current timer.
				RunTimer.Stop();
				// Restart it again.
				RunTimer.Start();
			}
		}

		private void DcControl_Load(object sender, EventArgs e)
		{
			OutputTextBox.Visibility = System.Windows.Visibility.Visible;
			OutputDataGridView.Visibility = System.Windows.Visibility.Collapsed;
			LanguageComboBox.SelectedIndex = 0;
			var autoRun = AutoRunCheckBox.IsChecked == true;
			if (autoRun)
				AutoRunCheckBox.IsChecked = false;
			var fi = new FileInfo(FileStatusLabel.Text);
			if (fi.Exists)
				LoadFile(FileStatusLabel.Text);
			if (autoRun)
				AutoRunCheckBox.IsChecked = true;
			UpdateStats();
		}


		private void RunTimer_Tick(object sender, EventArgs e)
		{
			RunTimer.Stop();
			loaded++;
			UpdateStats();
			LoadFile(FileStatusLabel.Text);
		}

		private void AutoLoadButton_Click(object sender, EventArgs e)
		{
			CodeFileSystemWatcher.EnableRaisingEvents = AutoLoadCheckBox.IsChecked == true;
			AutoLoadCheckBox.IsChecked = !AutoLoadCheckBox.IsChecked;
		}

		private void AutoRunButton_Click(object sender, EventArgs e)
		{
			AutoRunCheckBox.IsChecked = !AutoRunCheckBox.IsChecked;
		}

		private void ReloadButton_Click(object sender, EventArgs e)
		{
			LoadFile(FileStatusLabel.Text);
		}

		private void RunButton_Click(object sender, EventArgs e)
		{
			// we use a new thread to run it
			//new Thread(new ThreadStart(RunTheProg)).Start();
			if (!SupressRunDefaultFunction)
				Run("cde");
		}

		public bool SupressRunDefaultFunction;

		#region Events

		private void OpenButton_Click(object sender, EventArgs e)
		{
			var fi = new FileInfo(FileStatusLabel.Text);
			OpenCodeFileDialog.InitialDirectory = fi.DirectoryName;
			OpenCodeFileDialog.RestoreDirectory = true;
			OpenCodeFileDialog.DefaultExt = "*.cs; *.vb";
			if (OpenCodeFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
				return;
			FileStatusLabel.Text = OpenCodeFileDialog.FileName;
			LoadFile(FileStatusLabel.Text);
		}

		private void SaveButton_Click(object sender, EventArgs e)
		{

		}

		private void CheckButton_Click(object sender, EventArgs e)
		{
			Check();
		}

		private void ErrorsTextBox_TextChanged(object sender, EventArgs e)
		{
			if (ErrorsTextBox.Text.Length > 0)
			{
				ResultsTabControl.SelectedItem = ErrorsTabItem;
			}
		}

		private void OutputTextBox_TextChanged(object sender, EventArgs e)
		{
			if (OutputTextBox.Text.Length > 0)
			{
				ResultsTabControl.SelectedItem = OutputTabItem;
			}
		}

		private void LanguageComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			var rx = new Regex(@"\s*");
			var current = rx.Replace(CodeTextBox.Text.Trim(), "");
			var replace = string.IsNullOrEmpty(current);
			var keys = (LanguageType[])Enum.GetValues(typeof(LanguageType));
			for (var i = 0; i < keys.Length; i++)
			{
				var template = Templates[keys[i]];
				if (template is null)
					template = "";
				if (current == rx.Replace(template, ""))
				{
					replace = true;
					break;
				}
			}
			var language = (LanguageType)Enum.Parse(typeof(LanguageType), LanguageComboBox.Text);
			if (replace)
				CodeTextBox.Text = "";
			switch (language)
			{

				// List of languages.
				// https://github.com/icsharpcode/AvalonEdit/blob/master/ICSharpCode.AvalonEdit/Highlighting/Resources/Resources.cs
				case LanguageType.CSharp:
					CodeTextBox.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C#");
					break;
				case LanguageType.VB:
					CodeTextBox.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("VB");
					break;
				case LanguageType.JScript:
					CodeTextBox.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("JavaScript");
					break;
				default:
					CodeTextBox.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C#");
					break;
			}
			if (replace)
				CodeTextBox.Text = Templates[language];
			Check();
		}

		#endregion

		private void UpdateStats()
		{
			ChangedStatusLabel.Text = string.Format("Changed: {0}", changed);
			LoadedStatusLabel.Text = string.Format("Loaded: {0}", loaded);
		}

		private Dictionary<LanguageType, string> _Templates;

		private Dictionary<LanguageType, string> Templates
		{
			get
			{
				if (_Templates is null)
				{
					_Templates = new Dictionary<LanguageType, string>
					{
						{ LanguageType.CSharp, Helper.FindResource<string>(@"DynamicCompile.Templates.CSharp.cs") },
						{ LanguageType.JScript, Helper.FindResource<string>(@"DynamicCompile.Templates.JScript.js") },
						{ LanguageType.VB, Helper.FindResource<string>(@"DynamicCompile.Templates.VB.vb") }
					};
				}
				return _Templates;
			}
		}

		private readonly object providerLock = new object();


		private void ResultsTabControl_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (ResultsTabControl.SelectedItem == CodeTabItem)
			{
				var key = (LanguageType)Enum.Parse(typeof(LanguageType), LanguageComboBox.Text);
				CodeLineTextBox.Text = JocysCom.ClassLibrary.Text.Helper.ToLiteral(CodeTextBox.Text.Trim(), key.ToString());
			}
		}

	}
}
