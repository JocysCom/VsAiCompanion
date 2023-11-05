using System.Collections.Generic;
using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class FineTuningItem : FileListItem
	{
		public FineTuningItem()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		[DefaultValue("data.json")]
		public string JsonListFile { get => _JsonListFile; set => SetProperty(ref _JsonListFile, value); }
		string _JsonListFile;

		[DefaultValue("data.jsonl")]
		public string JsonLinesFile { get => _JsonLinesFile; set => SetProperty(ref _JsonLinesFile, value); }
		string _JsonLinesFile;

		[DefaultValue("")]
		public string SystemMessage { get => _SystemMessage; set => SetProperty(ref _SystemMessage, value); }
		string _SystemMessage;

		#region App Settings Selections

		public List<string> FineTuningSourceDataSelection { get => _FineTuningSourceDataSelection; set => SetProperty(ref _FineTuningSourceDataSelection, value); }
		List<string> _FineTuningSourceDataSelection;

		public List<string> FineTuningTuningDataSelection { get => _FineTuningTuningDataSelection; set => SetProperty(ref _FineTuningTuningDataSelection, value); }
		List<string> _FineTuningTuningDataSelection;

		public List<string> FineTuningRemoteDataSelection { get => _FineTuningRemoteDataSelection; set => SetProperty(ref _FineTuningRemoteDataSelection, value); }
		List<string> _FineTuningRemoteDataSelection;

		public List<string> FineTuningJobListSelection { get => _FineTuningJobListSelection; set => SetProperty(ref _FineTuningJobListSelection, value); }
		List<string> _FineTuningJobListSelection;

		public List<string> FineTuningModelListSelection { get => _FineTuningModelListSelection; set => SetProperty(ref _FineTuningModelListSelection, value); }
		List<string> _FineTuningModelListSelection;
		#endregion

	}
}
