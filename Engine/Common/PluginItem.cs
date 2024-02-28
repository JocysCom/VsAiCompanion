using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.ClassLibrary.Xml;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Xml.Serialization;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class PluginItem : INotifyPropertyChanged
	{

		public PluginItem() { }

		public PluginItem(System.Reflection.MethodInfo mi)
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
			AssemblyName = mi.DeclaringType.Assembly.GetName().FullName;
			Namespace = mi.DeclaringType.Namespace;
			Class = mi.DeclaringType.Name;
			ClassFullName = mi.DeclaringType.FullName;
			Name = mi.Name;
			RiskLevel = RiskLevel.Unknown;
			var rla = Attributes.FindCustomAttribute<RiskLevelAttribute>(mi);
			if (rla != null)
				RiskLevel = rla.Level;
			// Set icon.
			var iconName = Resources.Icons.Icons_Default.Icon_piece_grey;
			switch (RiskLevel)
			{
				case RiskLevel.Low:
					iconName = Resources.Icons.Icons_Default.Icon_piece_green;
					break;
				case RiskLevel.Medium:
					iconName = Resources.Icons.Icons_Default.Icon_piece_yellow;
					break;
				case RiskLevel.High:
					iconName = Resources.Icons.Icons_Default.Icon_piece_red;
					break;
				default:
					break;
			}
			Icon = Resources.Icons.Icons_Default.Current[iconName] as Viewbox;
			Id = (ClassFullName + "." + mi.Name).Trim('.');
			var summary = XmlDocHelper.RemoveSpaces(XmlDocHelper.GetSummaryText(mi));
			var returns = XmlDocHelper.RemoveSpaces(XmlDocHelper.GetReturnText(mi));
			var example = JocysCom.ClassLibrary.Text.Helper.RemoveIdent(
				XmlDocHelper.TrimSpaces(XmlDocHelper.GetExampleText(mi)));
			var lines = new List<string>();
			if (!string.IsNullOrEmpty(summary))
				lines.Add(summary);
			if (!string.IsNullOrEmpty(returns))
				lines.Add("Returns: " + returns);
			if (!string.IsNullOrEmpty(example))
				lines.Add("Example:\r\n" + example);
			Description = string.Join(Environment.NewLine, lines);
			Mi = mi;
			if (Params == null)
				Params = new BindingList<PluginParam>();
			var index = 0;
			foreach (var pi in mi.GetParameters())
			{
				var paramText = XmlDocHelper.RemoveSpaces(XmlDocHelper.GetParamText(mi, pi));
				var pp = new PluginParam();
				pp.Name = pi.Name;
				pp.IsOptional = pi.IsOptional;
				pp.Description = paramText;
				pp.Type = PluginsManager.GetJsonType(pi.ParameterType);
				pp.Index = index++;
				Params.Add(pp);
			}
		}

		/// <summary>Enable Plugin</summary>
		[DefaultValue(false)]
		public bool IsEnabled
		{
			get => _IsEnabled;
			set => SetProperty(ref _IsEnabled, value);
		}
		bool _IsEnabled;

		[DefaultValue("")]
		public string Id { get => _Id; set => SetProperty(ref _Id, value); }
		string _Id;

		[XmlIgnore]
		public Viewbox Icon { get => _Icon; set => SetProperty(ref _Icon, value); }
		Viewbox _Icon;

		[XmlIgnore]
		public System.Reflection.MethodInfo Mi { get => _Mi; set => SetProperty(ref _Mi, value); }
		System.Reflection.MethodInfo _Mi;

		[XmlIgnore]
		public BindingList<PluginParam> Params { get => _Params; set => SetProperty(ref _Params, value); }
		BindingList<PluginParam> _Params;


		[XmlIgnore]
		public string Namespace { get => _Namespace; set => SetProperty(ref _Namespace, value); }
		string _Namespace;

		[XmlIgnore]
		public RiskLevel RiskLevel { get => _RiskLevel; set => SetProperty(ref _RiskLevel, value); }
		RiskLevel _RiskLevel;

		[XmlIgnore]
		public string ClassFullName { get => _ClassFullName; set => SetProperty(ref _ClassFullName, value); }
		string _ClassFullName;

		[XmlIgnore]
		public string Class { get => _Class; set => SetProperty(ref _Class, value); }
		string _Class;

		[XmlIgnore]
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		[XmlIgnore]
		public string Description { get => _Description; set => SetProperty(ref _Description, value); }
		string _Description;

		[XmlIgnore]
		public string AssemblyName { get; set; }

		#region INotifyPropertyChanged

		// CWE-502: Deserialization of Untrusted Data
		// Fix: Apply [field: NonSerialized] attribute to an event inside class with [Serialized] attribute.
		[field: NonSerialized]
		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			if (handler != null)
			{
				if (ControlsHelper.MainTaskScheduler == null)
					handler(this, new PropertyChangedEventArgs(propertyName));
				else
					ControlsHelper.Invoke(handler, this, new PropertyChangedEventArgs(propertyName));
			}
		}

		protected void SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		#endregion

	}
}
