using JocysCom.ClassLibrary.Controls;
using JocysCom.VS.AiCompanion.Plugins.Core;
using JocysCom.VS.AiCompanion.Plugins.Core.Server;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JocysCom.VS.AiCompanion.Engine.Controls.Options
{
	/// <summary>
	/// Interaction logic for UdpServerControl.xaml
	/// </summary>
	public partial class UdpServerControl : UserControl
	{
		public UdpServerControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			DataContext = this;
			Global.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
		}

		#region UDP Server

		private UdpServer<VisualStudio> _udpServer;
		private int messagesCount;
		public bool IsServerEnabled
		{
			get => _udpServer != null && _udpServer.IsRunning;
			set
			{
				if (value)
					StartServer();
				else
					StopServer();
			}
		}

		public int StartPort { get; set; }
		public int EndPort { get; set; }
		public int MessagesCount
		{
			get => messagesCount;
			set
			{
				messagesCount = value;
				Dispatcher.Invoke(() => MessagesTextBox.Text = value.ToString());
			}
		}

		private void StartServer()
		{
			_udpServer = new UdpServer<VisualStudio>();
			_udpServer.StartServer(null, StartPort);
			_udpServer.MessageReceived += (sender, e) => MessagesCount++;
		}

		private void StopServer()
		{
			_udpServer?.StopServer();
		}

		#endregion

		#region UDP Client

		private UdpClient<VisualStudio> _udpClient;
		private DispatcherTimer ScanTimer;

		public bool IsClientEnabled
		{
			get => _udpClient != null;
			set
			{
				if (value)
				{
					StartClient();
					ScanServers();
					ScanTimer.Start();
				}
				else
				{
					StopClient();
					ScanTimer.Stop();
				}
			}
		}

		private Dictionary<ushort, string> availableServers;
		public Dictionary<ushort, string> AvailableServers
		{
			get => availableServers;
			private set
			{
				availableServers = value;
				Dispatcher.Invoke(() =>
				{
					ServerSelectionComboBox.ItemsSource = value;
				});
			}
		}

		public KeyValuePair<ushort, string>? SelectedServer { get; set; }

		private void StartClient()
		{
			_udpClient = new UdpClient<VisualStudio>(UdpHelper.DefaultIPAddress);
		}

		private void StopClient()
		{
			_udpClient = null;
		}

		private void ScanButton_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			ScanServers();
		}

		private void ScanServers()
		{
			Task.Run(() =>
			{
				if (_udpClient != null)
				{
					var servers = _udpClient.ScanServers();
					AvailableServers = servers;
				}
			});
		}

		private void ScanTimer_Tick(object sender, EventArgs e)
		{
			ScanServers();
		}

		#endregion

		private void AppSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				default:
					break;
			}
		}


		private void This_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				UiPresetsManager.InitControl(this, true);
			}
		}
	}
}
