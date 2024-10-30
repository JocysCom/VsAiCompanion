using DiffPlex;
using DiffPlex.DiffBuilder;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Speech;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for PluginApprovalControl.xaml
	/// </summary>
	public partial class PluginApprovalControl : UserControl, INotifyPropertyChanged
	{
		public PluginApprovalControl()
		{
			_item = emptyData;
			InitializeComponent();
			ApprovalColor = Resources["BackgroundDark"] as Brush;
			if (ControlsHelper.IsDesignMode(this))
				return;
			//PluginItemPanel.MethodCheckBox.IsEnabled = false;
			PluginItemPanel.MethodCheckBox.Visibility = Visibility.Collapsed;
			PluginItemPanel.MethodStackPanel.Visibility = Visibility.Visible;

			VoiceCommands = new VoiceCommands(ApproveCommand, DenyCommand);
			Global.AppSettings.PropertyChanged += Global_AppSettings_PropertyChanged;
			VoiceCommands.CommandRecognized += VoiceCommands_CommandRecognized;
		}

		private void Global_AppSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Global.AppSettings.EnableVoiceToApprovePlugins))
				UpdatePluginItemControl();
		}

		VoiceCommands VoiceCommands;

		BindingList<PluginApprovalItem> emptyData = new BindingList<PluginApprovalItem>();

		BindingList<PluginApprovalItem> _item;
		public BindingList<PluginApprovalItem> Item
		{
			get => _item;
			set
			{
				if (Equals(value, _item))
					return;
				// Set new item.
				if (_item != null)
				{
					_item.ListChanged -= _item_ListChanged;
				}
				_item = value ?? emptyData;
				DataContext = _item;
				_item.ListChanged += _item_ListChanged;
				UpdatePluginItemControl();
			}
		}

		Dictionary<RiskLevel, Brush> RiskToBrush = new Dictionary<RiskLevel, Brush>()
		{
			{ RiskLevel.Unknown, (Brush)brushConverter.ConvertFromString("#90dedede") },
			{ RiskLevel.None, (Brush)brushConverter.ConvertFromString("#90278fcb") },
			{ RiskLevel.Low, (Brush)brushConverter.ConvertFromString("#9057a01c") },
			{ RiskLevel.Medium, (Brush)brushConverter.ConvertFromString("#90ffcc00") },
			{ RiskLevel.High, (Brush)brushConverter.ConvertFromString("#90d75f00") },
			{ RiskLevel.Critical, (Brush)brushConverter.ConvertFromString("#90c70000") },
		};

		public static BrushConverter brushConverter = new BrushConverter();


		#region Plugin Approvals

		private void _item_ListChanged(object sender, ListChangedEventArgs e)
		{
			UpdatePluginItemControl();
		}

		void UpdatePluginItemControl()
		{
			//PluginItemPanel.Args = ApprovalItem?.Args;
			OnPropertyChanged(nameof(SowApprovalPanel));
			OnPropertyChanged(nameof(ApprovalItem));
			OnPropertyChanged(nameof(ShowSecondaryAiEvaluation));
			OnPropertyChanged(nameof(FunctionId));
			if (Item.Count > 0 && Global.AppSettings.EnableVoiceToApprovePlugins)
			{
				VoiceCommands.StartListening();
			}
			else
			{
				VoiceCommands.StopListening();
			}
		}

		public PluginApprovalItem ApprovalItem
		{
			get
			{
				var item = Item.FirstOrDefault();
				var rl = item?.Plugin.RiskLevel;
				var brush = RiskToBrush[RiskLevel.Unknown];
				if (item?.Plugin.Name == nameof(Plugins.Core.UnifiedFormat.IDiffHelper.CompareContentsAndReturnChanges))
				{
					var originalText = (string)item.Plugin.InvokeParams[0];
					var modifiedText = (string)item.Plugin.InvokeParams[1];
					DiffViewer.SideBySideModeToggleTitle = false;
					DiffViewer.IsCommandBarVisible = true;
					SetDiff(originalText, modifiedText);
					ControlsHelper.SetVisible(DiffPanel, true);
				}
				else if (item?.Plugin.Name == nameof(Plugins.Core.UnifiedFormat.IDiffHelper.ModifyContents))
				{
					var contents = (string)item.Plugin.InvokeParams[0];
					var unifiedDiff = (string)item.Plugin.InvokeParams[1];
					//var oldText = item.function.parameters[].Args[0].ToString();
					//var newText = item.Args[1].ToString();
					//SetDiff(oldText, newText);
					//ControlsHelper.SetVisible(DiffPanel, true);
				}
				else
				{
					ControlsHelper.SetVisible(DiffPanel, false);
					SetDiff("", "");

				}
				if (rl != null && RiskToBrush.ContainsKey(rl.Value))
					brush = RiskToBrush[rl.Value];
				ApprovalColor = brush;
				OnPropertyChanged(nameof(ApprovalColor));
				return item;
			}
		}

		public Brush ApprovalColor { get; set; } = SystemColors.ControlDarkBrush;

		public Visibility SowApprovalPanel => Item.Count > 0
			? Visibility.Visible : Visibility.Collapsed;

		public Visibility ShowSecondaryAiEvaluation => string.IsNullOrEmpty(ApprovalItem?.SecondaryAiEvaluation)
			? Visibility.Collapsed : Visibility.Visible;

		public string FunctionId => ApprovalItem?.function?.id ?? string.Empty;

		#endregion

		//// It is up to user now to approve.
		//var text = "Do you want to execute function submitted by AI?";
		//		if (!string.IsNullOrEmpty(assistantEvaluation))
		//			text += assistantEvaluation;
		//		text += "\r\n\r\n" + Client.Serialize(function);
		//		var caption = $"{Global.Info.Product} - Plugin Function Approval";

		private void ApproveButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Approve(true);
		}

		private void DenyButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			Approve(false);
		}

		async void Approve(bool isApproved)
		{
			var item = ApprovalItem;
			var button = isApproved ? ApproveButton : DenyButton;
			var defaultBackground = button.Background;
			button.Background = Resources["BackgroundDarkPressed"] as Brush;
			await ClassLibrary.Helper.Debounce(new System.Action(() =>
			{
				button.Background = defaultBackground;
				item.IsApproved = isApproved;
				item.Semaphore.Release();
			}));
		}

		string ApproveCommand = "approve";
		string DenyCommand = "deny";

		private void VoiceCommands_CommandRecognized(string command)
		{
			var isApproved = command == ApproveCommand;
			Approve(isApproved);
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}

		#region Diff Panel

		public void SetDiff(string oldText, string newText)
		{
			var diffBuilder = new SideBySideDiffBuilder(new Differ());
			var diffModel = diffBuilder.BuildDiffModel(oldText, newText);
			DiffViewer.OldText = oldText;
			DiffViewer.NewText = newText;
		}

		public string GetDiffHTml(string oldText, string newText)
		{
			var diffBuilder = new SideBySideDiffBuilder(new Differ());
			var diffModel = diffBuilder.BuildDiffModel(oldText, newText);
			var html = new StringBuilder();
			html.Append("<html><body><table>");
			// Iterate over each line in the diff model
			foreach (var diffLine in diffModel.NewText.Lines)
			{
				string lineClass = diffLine.Type.ToString().ToLower();
				html.AppendFormat("<tr class='{0}'><td>{1}</td></tr>", lineClass, diffLine.Text);
			}
			return html.ToString();
		}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		#endregion

	}
}
