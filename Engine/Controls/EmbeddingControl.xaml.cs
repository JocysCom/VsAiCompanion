using Embeddings.DataAccess;
using Embeddings.Model;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Engine.Companions.ChatGPT;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Linq;

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
	public partial class EmbeddingControl : UserControl, INotifyPropertyChanged
	{
		public EmbeddingControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			//Global.AppSettings.Embedding.AiModel = "text-embedding-ada-002";
			Item = Global.AppSettings.Embedding;
#if NETFRAMEWORK
#else
			EditButton.Visibility = System.Windows.Visibility.Collapsed;
#endif
		}

		private void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var path = AssemblyInfo.ParameterizePath(Item.Source, true);
			ControlsHelper.OpenUrl(path);
		}

		private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		public EmbeddingSettings Item
		{
			get => _Item;
			set
			{
				if (_Item != null)
				{
					_Item.PropertyChanged -= _Item_PropertyChanged;
				}
				_Item = value;
				if (value != null)
				{
					_Item.PropertyChanged += _Item_PropertyChanged;
				}
				DataContext = value;
				AiModelBoxPanel.Item = value;
				OnPropertyChanged(nameof(Item));
				//OnPropertyChanged(nameof(FilteredConnectionString));
			}
		}
		EmbeddingSettings _Item;

		private void _Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(EmbeddingSettings.Source))
			{
				//OnPropertyChanged(nameof(FilteredConnectionString));
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

		private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{

		}

		private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			//#if NETFRAMEWORK
			Microsoft.Data.ConnectionUI.DataConnectionDialog dcd;
			dcd = new Microsoft.Data.ConnectionUI.DataConnectionDialog();
			//Adds all the standard supported databases
			//DataSource.AddStandardDataSources(dcd);
			//allows you to add datasources, if you want to specify which will be supported 
			dcd.DataSources.Add(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource);
			dcd.SetSelectedDataProvider(Microsoft.Data.ConnectionUI.DataSource.SqlDataSource, Microsoft.Data.ConnectionUI.DataProvider.SqlDataProvider);
			dcd.ConnectionString = Item.Target ?? "";
			Microsoft.Data.ConnectionUI.DataConnectionDialog.Show(dcd);
			if (dcd.DialogResult == System.Windows.Forms.DialogResult.OK)
			{
				Item.Target = dcd.ConnectionString;
			}
			//OnPropertyChanged(nameof(FilteredConnectionString));
			//#endif
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
				var input = new List<string> { Item.Message };
				var client = new Client(Item.AiService);
				var results = await client.GetEmbedding(Item.AiModel, input);
#if NETFRAMEWORK
				var db = new Embeddings.DataAccess.EmbeddingsContext();
				db.Database.Connection.ConnectionString = Item.Target;
#else
				var db = EmbeddingsContext.Create(Item.Target);
#endif


				// Example values for skip and take
				int skip = 0;
				int take = 2;

				var vectors = results[0];

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
				var similarFiles = db.Database.SqlQuery<FileEmbedding>(
					sqlCommand, embeddingParam, skipParam, takeParam)
					.ToList();
#else
				var similarFiles = db.FileEmbeddings.FromSqlRaw(
					sqlCommand, embeddingParam, skipParam, takeParam)
					.ToList();
#endif
				foreach (var item in similarFiles)
				{
					item.Embedding = null;
				}

				var json = Client.Serialize(similarFiles);
				LogTextBox.Text = json;
			}
			catch (System.Exception ex)
			{
				LogTextBox.Text = ex.ToString();
			}
		}
	}
}
