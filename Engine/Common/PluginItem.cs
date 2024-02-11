using JocysCom.ClassLibrary.Controls;
using JocysCom.ClassLibrary.Xml;
using JocysCom.VS.AiCompanion.Engine.Plugins;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
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
			Id = mi.DeclaringType.FullName;
			Name = mi.Name;
			Description = XmlDocHelper.GetSummaryText(mi);
			if (Params == null)
				Params = new BindingList<PluginParam>();
			foreach (var pi in mi.GetParameters())
			{
				var paramText = XmlDocHelper.GetParamText(mi, pi).Trim(new char[] { '\r', '\n', ' ' });
				var pp = new PluginParam();
				pp.Name = pi.Name;
				pp.IsOptional = pi.IsOptional;
				pp.Description = paramText;
				pp.Type = PluginsManager.GetJsonType(pi.ParameterType);
				Params.Add(pp);
			}
		}

		/// <summary>Enable Plugin</summary>
		[DefaultValue(false)]
		public bool IsEnabled { get => _IsEnabled; set => SetProperty(ref _IsEnabled, value); }
		bool _IsEnabled;

		[DefaultValue("")]
		public string Id { get => _Id; set => SetProperty(ref _Id, value); }
		string _Id;

		[XmlIgnore]
		public DrawingImage Icon { get => _Icon; }
		DrawingImage _Icon;

		[XmlIgnore]
		public BindingList<PluginParam> Params { get => _Params; set => SetProperty(ref _Params, value); }
		BindingList<PluginParam> _Params;

		[XmlIgnore]
		public string Name { get => _Name; set => SetProperty(ref _Name, value); }
		string _Name;

		[XmlIgnore]
		public string Description { get => _Description; set => SetProperty(ref _Description, value); }
		string _Description;

		[XmlIgnore]
		public string AssemblyName { get; set; }
		[XmlIgnore]
		public string ClassName { get; set; }

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
