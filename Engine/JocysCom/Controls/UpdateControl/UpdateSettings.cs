using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace JocysCom.ClassLibrary.Controls.UpdateControl
{
	public class UpdateSettings : JocysCom.ClassLibrary.ComponentModel.NotifyPropertyChanged
	{

		public UpdateSettings()
		{
			JocysCom.ClassLibrary.Runtime.Attributes.ResetPropertiesToDefault(this);
		}
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

		/// <summary>Company path name on GitHub.</summary>
		[DefaultValue(null)]
		public string GitHubCompany { get => _GitHubCompany; set => SetProperty(ref _GitHubCompany, value); }
		string _GitHubCompany;

		/// <summary>Product path name on GitHub.</summary>
		[DefaultValue(null)]
		public string GitHubProduct { get => _GitHubProduct; set => SetProperty(ref _GitHubProduct, value); }
		string _GitHubProduct;

		/// <summary>
		/// Make sure application is signed with a valid certificate.
		/// </summary>
		[DefaultValue(true)]
		public bool VerifySignature { get => _VerifySignature; set => SetProperty(ref _VerifySignature, value); }
		bool _VerifySignature;

		/// <summary>
		/// Include preview releases in downloads.
		/// </summary>
		[DefaultValue(false)]
		public bool IncludePreview { get => _IncludePreview; set => SetProperty(ref _IncludePreview, value); }
		bool _IncludePreview;

		/// <summary>
		/// Check Version
		/// </summary>
		[DefaultValue(null)]
		public bool CheckVersion { get => _CheckVersion; set => SetProperty(ref _CheckVersion, value); }
		bool _CheckVersion;

		/// <summary>
		/// Include prerelease.
		/// </summary>
		public bool IncludePrerelease { get => _IncludePrerelease; set => SetProperty(ref _IncludePrerelease, value); }
		bool _IncludePrerelease;

		/// <summary>
		/// Filter by minimum version.
		/// </summary>
		public string MinVersion { get => _MinVersion; set => SetProperty(ref _MinVersion, value); }
		string _MinVersion;

		/// <summary>
		/// GitHub asset name to download.
		/// Executing asset name + ".zip".
		/// </summary>
		[DefaultValue(null)]
		public string GitHubAssetName { get => _GitHubAssetName; set => SetProperty(ref _GitHubAssetName, value); }
		string _GitHubAssetName;

		/// <summary>
		/// Executable name to extract from the .zip asset.
		/// </summary>
		[DefaultValue(null)]
		public string FileNameInsideZip { get => _FileNameInsideZip; set => SetProperty(ref _FileNameInsideZip, value); }
		string _FileNameInsideZip;

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
