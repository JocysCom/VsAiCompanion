using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Linq;
using System.Threading.Tasks;
using Embeddings;
#if NETFRAMEWORK
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
#endif

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for EmbeddingControl.xaml
	/// </summary>
	public partial class EmbeddingsItemControl : UserControl, INotifyPropertyChanged
	{
		public EmbeddingsItemControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
		}

		#region List Panel Item

		TaskSettings PanelSettings { get; set; } = new TaskSettings();

		[Category("Main"), DefaultValue(ItemType.None)]
		public ItemType DataType
		{
			get => _DataType;
			set
			{
				_DataType = value;
				if (ControlsHelper.IsDesignMode(this))
					return;
				// Update panel settings.
				PanelSettings.PropertyChanged -= PanelSettings_PropertyChanged;
				PanelSettings = Global.AppSettings.GetTaskSettings(value);
				PanelSettings.PropertyChanged += PanelSettings_PropertyChanged;
				// Update the rest.
				//PanelSettings.UpdateBarToggleButtonIcon(BarToggleButton);
				PanelSettings.UpdateListToggleButtonIcon(ListToggleButton);
				//OnPropertyChanged(nameof(BarPanelVisibility));
			}
		}
		private ItemType _DataType;

		private async void PanelSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			await Task.Delay(0);
		}

		private void ListToggleButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			PanelSettings.UpdateListToggleButtonIcon(ListToggleButton, true);
		}

		#endregion

		private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var path = AssemblyInfo.ParameterizePath(Item.Source, true);
			ControlsHelper.OpenUrl(path);
		}

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		public EmbeddingsItem Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
				}
				AiModelBoxPanel.Item = null;
				_Item = value;
				AiModelBoxPanel.Item = value;
				if (value != null)
				{
					value.PropertyChanged += _Item_PropertyChanged;
				}
				IconPanel.BindData(value);
				OnPropertyChanged(nameof(Item));
			}
		}
		EmbeddingsItem _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(EmbeddingsItem.Source))
			{
			}
		}

		private async void ProcessSettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			try
			{
				var path = AssemblyInfo.ExpandPath(Item.Source);
				await EmbeddingHelper.ConvertToEmbeddingsCSV(
					path,
					Item.Target,
					Item.AiService, Item.AiModel);
			}
			catch (System.Exception ex)
			{
				LogTextBox.Text = ex.ToString();
			}
		}

		System.Windows.Forms.FolderBrowserDialog _FolderBrowser = new System.Windows.Forms.FolderBrowserDialog();

		private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

			var dialog = _FolderBrowser;
			dialog.SelectedPath = Item.Source;
			DialogHelper.FixDialogFolder(dialog, Global.FineTuningPath);
			var result = dialog.ShowDialog();
			if (result != System.Windows.Forms.DialogResult.OK)
				return;
			Item.Source = dialog.SelectedPath;
		}

		private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
#if NETFRAMEWORK
			Microsoft.Data.ConnectionUI.DataConnectionDialog dcd;
			dcd = new Microsoft.Data.ConnectionUI.DataConnectionDialog();
			//Adds all the standard supported databases
			//DataSource.AddStandardDataSources(dcd);
			//allows you to add datasources, if you want to specify which will be supported 
			dcd.DataSources.Add(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource);
			dcd.SetSelectedDataProvider(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource, Microsoft.Data.ConnectionUI.DataProvider.SqlDataProvider);
			try
			{
				dcd.ConnectionString = Item.Target ?? "";
			}
			catch (System.Exception)
			{
			}
			Microsoft.Data.ConnectionUI.DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
			{
				Item.Target = dcd.ConnectionString;
			}
			//OnPropertyChanged(nameof(FilteredConnectionString));
#else
			Microsoft.SqlServer.Management.ConnectionUI.DataConnectionDialog dcd;
			dcd = new Microsoft.SqlServer.Management.ConnectionUI.DataConnectionDialog();
			//Adds all the standard supported databases
			//DataSource.AddStandardDataSources(dcd);
			//allows you to add datasources, if you want to specify which will be supported 
			dcd.DataSources.Add(Microsoft.SqlServer.Management.ConnectionUI.DataSource.SqlDataSource);
			dcd.SetSelectedDataProvider(Microsoft.SqlServer.Management.ConnectionUI.DataSource.SqlDataSource, Microsoft.SqlServer.Management.ConnectionUI.DataProvider.SqlDataProvider);
			try
			{
				dcd.ConnectionString = Item.Target ?? "";
			}
			catch (System.Exception)
			{
			}
			Microsoft.SqlServer.Management.ConnectionUI.DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
			{
				Item.Target = dcd.ConnectionString;
			}
			//OnPropertyChanged(nameof(FilteredConnectionString));

#endif
		}

		#region Database Connection Strings

		///// <summary>
		///// Database Administrative Connection String.
		///// </summary>
		//public string FilteredConnectionString
		//{
		//	get
		//	{
		//		var value = Global.AppSettings.Embedding.Target;
		//		if (string.IsNullOrWhiteSpace(value))
		//			return "";
		//		var filtered = ClassLibrary.Data.SqlHelper.FilterConnectionString(value);
		//		return filtered;
		//	}
		//	set
		//	{
		//	}
		//}

		#endregion

		#region ■ INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		#endregion

		private async void SearchButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			try
			{
				LogTextBox.Text = "Converting message to embedding vectors...";
				var input = new List<string> { Item.Message };
				var client = new Client(Item.AiService);
				var results = await client.GetEmbedding(Item.AiModel, input);
				LogTextBox.Text = " Done.\r\n";
#if NETFRAMEWORK
				var db = new EmbeddingsContext();
				db.Database.Connection.ConnectionString = Item.Target;
#else
				var db = EmbeddingsContext.Create(Item.Target);
#endif
				// Example values for skip and take
				int skip = 0;
				int take = 2;

				var vectors = results[0];
				LogTextBox.Text = "Searching on database...";
				// Convert your embedding to the format expected by SQL Server.
				// This example assumes `results` is the embedding in a suitable binary format.
				var embeddingParam = new SqlParameter("@promptEmbedding", SqlDbType.VarBinary)
				{
					Value = EmbeddingHelper.VectorToBinary(vectors)
				};

				var skipParam = new SqlParameter("@skip", SqlDbType.Int) { Value = skip };
				var takeParam = new SqlParameter("@take", SqlDbType.Int) { Value = take };

				// Assuming `FileSimilarity` is the result type.
				var sqlCommand = "EXEC [Embedding].[sp_getSimilarFileEmbeddings] @promptEmbedding, @skip, @take";
#if NETFRAMEWORK
				var similarFiles = db.Database.SqlQuery<Embeddings.Embedding.FilePart>(
					sqlCommand, embeddingParam, skipParam, takeParam)
					.ToList();
#else
				var similarFiles = db.FileParts.FromSqlRaw(
					sqlCommand, embeddingParam, skipParam, takeParam)
					.ToList();
#endif
				LogTextBox.Text = " Done...";
				foreach (var item in similarFiles)
				{
					var file = db.Files.Where(x => x.Id == item.Id).FirstOrDefault();
					LogTextBox.Text += $"\r\n{file?.Url}";
					var text = JocysCom.ClassLibrary.Text.Helper.IdentText(item.Text);
					LogTextBox.Text += "\r\n" + text + "\r\n\r\n";
				}
				//var json = Client.Serialize(similarFiles);
				//LogTextBox.Text = json;
			}
			catch (System.Exception ex)
			{
				LogTextBox.Text = ex.ToString();
			}
		}
	}
}
