using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	/// <summary>
	/// Represents the settings used for updating the application.
	/// </summary>
	public class UpdateSettings : JocysCom.ClassLibrary.ComponentModel.NotifyPropertyChanged
	{
		public UpdateSettings()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}

		/// <summary>
		/// Updates missing default values for the settings properties based on the provided assembly information.
		/// </summary>
		/// <param name="assembly">The assembly to extract default values from. If null, the entry assembly is used.</param>
		public void UpdateMissingDefaults(Assembly assembly = null)
		{
			assembly = assembly ?? Assembly.GetEntryAssembly();
			var n = new UpdateSettings();
			if (string.IsNullOrWhiteSpace(GitHubCompany))
			{
				var company = GetAttribute<AssemblyCompanyAttribute>(assembly, a => a.Company);
				GitHubCompany = ConvertString(company);
			}
			if (string.IsNullOrWhiteSpace(GitHubProduct))
			{
				var product = GetAttribute<AssemblyProductAttribute>(assembly, a => a.Product);
				GitHubProduct = ConvertString(product);
			}
			if (string.IsNullOrWhiteSpace(GitHubAssetName))
			{
				GitHubAssetName = assembly.GetName().Name + ".zip";
			}
			if (string.IsNullOrWhiteSpace(FileNameInsideZip))
			{
				FileNameInsideZip = assembly.GetName().Name + ".exe";
			}
			if (string.IsNullOrWhiteSpace(MinVersion))
			{
				MinVersion = assembly.GetName().Version.ToString();
			}
		}

		/// <summary>
		/// Company name used in the GitHub repository path.
		/// </summary>
		[DefaultValue(null)]
		public string GitHubCompany { get => _GitHubCompany; set => SetProperty(ref _GitHubCompany, value); }
		string _GitHubCompany;

		/// <summary>
		/// Product name used in the GitHub repository path.
		/// </summary>
		[DefaultValue(null)]
		public string GitHubProduct { get => _GitHubProduct; set => SetProperty(ref _GitHubProduct, value); }
		string _GitHubProduct;

		/// <summary>
		/// Determines whether the application should verify that the update is signed with a valid digital signature.
		/// </summary>
		[DefaultValue(true)]
		public bool VerifySignature { get => _VerifySignature; set => SetProperty(ref _VerifySignature, value); }
		bool _VerifySignature;

		/// <summary>
		/// If true, includes preview releases in the update search.
		/// </summary>
		[DefaultValue(false)]
		public bool IncludePreview { get => _IncludePreview; set => SetProperty(ref _IncludePreview, value); }
		bool _IncludePreview;

		/// <summary>
		/// Determines whether to check the version of the application during the update process.
		/// </summary>
		[DefaultValue(null)]
		public bool CheckVersion { get => _CheckVersion; set => SetProperty(ref _CheckVersion, value); }
		bool _CheckVersion;

		/// <summary>
		/// If true, includes pre-release versions in the update search.
		/// </summary>
		[DefaultValue(false)]
		public bool IncludePrerelease { get => _IncludePrerelease; set => SetProperty(ref _IncludePrerelease, value); }
		bool _IncludePrerelease;

		/// <summary>
		/// Specifies the minimum version required for updates.
		/// </summary>
		[DefaultValue(null)]
		public string MinVersion { get => _MinVersion; set => SetProperty(ref _MinVersion, value); }
		string _MinVersion;

		/// <summary>
		/// The name of the asset file to download from GitHub.
		/// </summary>
		[DefaultValue(null)]
		public string GitHubAssetName { get => _GitHubAssetName; set => SetProperty(ref _GitHubAssetName, value); }
		string _GitHubAssetName;

		/// <summary>
		/// The name of the executable file inside the downloaded package (e.g., zip file).
		/// </summary>
		[DefaultValue(null)]
		public string FileNameInsideZip { get => _FileNameInsideZip; set => SetProperty(ref _FileNameInsideZip, value); }
		string _FileNameInsideZip;

		/// <summary>
		/// List of versions that the user has chosen to skip.
		/// </summary>
		[DefaultValue(null)]
		public List<string> SkippedVersions
		{
			get => _SkippedVersions = _SkippedVersions ?? new List<string>();
			set => SetProperty(ref _SkippedVersions, value);
		}
		private List<string> _SkippedVersions;

		/// <summary>Indicates whether the property should be serialized with the XML serializer.</summary>
		public bool ShouldSerializeSkippedVersions() => !SkippedVersions.Any();

		#region Helper Methods

		private static string ConvertString(string s)
		{
			s = NonLetters.Replace(s, " ");
			s = s.ToLower();
			s = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);
			s = s.Replace(" ", "");
			return s;
		}

		private static Regex NonLetters = new Regex("[^a-zA-Z]+", RegexOptions.Compiled);
		private static string GetAttribute<T>(Assembly assembly, Func<T, string> value) where T : Attribute
		{
			T attribute = (T)Attribute.GetCustomAttribute(assembly, typeof(T));
			return attribute is null
				? ""
				: value.Invoke(attribute);
		}

		#endregion
	}
}
