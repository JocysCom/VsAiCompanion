// <#+ /*
#if NET6_0_OR_GREATER
#nullable disable
#endif
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

// */
//----BEGIN T4 CODE-----

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class MainModel {

	/// <summary>
	/// Path to log file.
	/// </summary>
	public static FileInfo LogFile;

	/// <summary>
	/// Path to *.tt file.
	/// </summary>
	public static FileInfo TemplateFile;

	/// <summary>
	/// *.tt.config
	/// </summary>
	public static Config config;

	private static Version GeneratorVersion = new Version(2023, 1, 19);

	private const string Error = "Error";
	private const string Warning = "Warning";

	private static Pluralizer pluralizer = new Pluralizer();

	private static long oldFilesSize;
	private static long newFilesSize;

	public static void InitTemplates(string hostTemplateFile) {
		var scriptStopwatch = System.Diagnostics.Stopwatch.StartNew();
		var file = new FileInfo(hostTemplateFile);
		TemplateFile = file;
		// Initialize log file.	
		var scriptName = System.IO.Path.GetFileNameWithoutExtension(file.FullName);
		LogFile = new FileInfo(file.Directory.FullName + "\\" + scriptName + ".log");
		if (LogFile.Exists)
			LogFile.Delete();
		var title = $"{ConvertNameToString(nameof(MainModel))} Code Generator";
		SetTitle(title);
		var configs = file.Directory.GetFiles($"{file.Name}*.config");
		foreach (var config in configs) {
			Log(new string('-', 64));
			Log($"CONFIG: {config.Name}");
			Log(new string('-', 64));
			SetTitle($"{title} - {config.Name}");
			MainModel.config = new Config();
			MainModel.config.LoadConfig(config);
			Log($"CONFIG Loaded");
			if (!MainModel.config.Enabled) {
				Log($"Generator is disabled in {config.Name}");
				continue;
			}
			// Get total size of current files.
			var outDi = new DirectoryInfo(MainModel.config.OutputPath);
			if (outDi.Exists)
				oldFilesSize += outDi.GetFiles("*.*", SearchOption.AllDirectories).Sum(x => x.Length);

			// Generate enumerations.
			GenerateEnums("Types");
			// Generate classes. Exclude "Types" schema.
			GenerateClasses(GenType.Class, false, "Interfaces", "Types");
			GenerateClasses(GenType.Interface, true, "Interfaces");
			// Get total size of current files.
			newFilesSize += outDi.GetFiles("*.*", SearchOption.AllDirectories).Sum(x => x.Length);
		}
		Log(new string('-', 64));
		SetTitle($"{title} - Done");
		var oldSize = oldFilesSize / 1024;
		var newSize = newFilesSize / 1024;
		var difSize = newSize - oldSize;
		var oldHours = GetHoursOfCoding(oldFilesSize);
		var newHours = GetHoursOfCoding(newFilesSize);
		var difHours = newHours - oldHours;
		var oldCost = GetPriceOfCoding(oldFilesSize);
		var newCost = GetPriceOfCoding(newFilesSize);
		var difCost = newCost - oldCost;
		Log("");
		Log($"Based on average developer writing 25,000 lines (43 bytes per line) per year.");
		Log($"Old Size: {oldSize,6:#,##0} KB, Coding Hours: {oldHours,6:#,##0}, Manual Cost: {oldCost,8:£#,##0}");
		Log($"New Size: {newSize,6:#,##0} KB, Coding Hours: {newHours,6:#,##0}, Manual Cost: {newCost,8:£#,##0}");
		Log($"Change:   {difSize,6:+#,##0;-#,##0;#,##0} KB, Coding Hours: {difHours,6:+#,##0;-#,##0;#,##0}, Manual Cost: {difCost,8:£+#,##0;-#,##0;#,##0}");
		var scriptStopWatchElapsed = scriptStopwatch.Elapsed;
		// Efficiency = Output (useful energy) ÷ Input (total energy).
		// Productivity = (output volume) ÷ (input volume).
		// Input volume (work hours): 8 Hours to modify the script + waiting for output.
		var inputWork = (decimal)scriptStopWatchElapsed.TotalHours + 8;
		// Output volume (work hours).
		var outputWork = newHours;
		var productivity = outputWork / inputWork;
		Log("");
		Log($"Script Time:         {scriptStopWatchElapsed}");
		Log($"Script Productivity: {productivity:#,##0} times more productive than manual coding.");
		var efficiency = outputWork / (outputWork + inputWork);
		Log($"Script Efficiency:   {efficiency:#,##0.00%}.");
		Log("");
	}

	public static void SetTitle(string s) {
		// Return if console is not available.
		if (Console.LargestWindowWidth == 0)
			return;
		Console.Title = s;
	}

	public static decimal GetPriceOfCoding(long bytes) {
		// £20 multiplied by cost to the company (2x).
		return GetHoursOfCoding(bytes) * 20m * 2m;
	}

	public static decimal GetHoursOfCoding(long bytes) {
		return GetYearsOfCoding(bytes) * 365.25m * 8;
	}

	public static decimal GetYearsOfCoding(long bytes) {
		// Speed of avergae developers.
		var linesPerYear = 25000;
		var bytesPerLine = 43;
		var bytesPerYear = linesPerYear * bytesPerLine;
		return (decimal)bytes / bytesPerYear;
	}

	public static string GetPath(string name) {
		var combined = System.IO.Path.Combine(config.OutputPath, name);
		// Fix dot notations.
		combined = Path.GetFullPath(combined);
		return combined;
	}

	public static string ConvertNameToString(string s) {
		// Add space before uppercase.
		var ucRx = new Regex("([A-Z][^A-Z])", RegexOptions.Multiline);
		s = ucRx.Replace(s, " $1").Trim();
		// Replace with spaces.
		var spRx = new Regex("[_ ]+", RegexOptions.Multiline);
		s = spRx.Replace(s, " ").Trim();
		return s;
	}

	/// <summary>
	/// Log to file and write error to console.
	/// </summary>
	public static void Log(string format, params object[] args) {
		var content = args.Length == 0 ? format : string.Format(format, args);
		var isError = content.TrimStart().StartsWith(Error, StringComparison.OrdinalIgnoreCase);
		var isWarning = content.TrimStart().StartsWith(Warning, StringComparison.OrdinalIgnoreCase);
		if (isError)
			Console.ForegroundColor = ConsoleColor.Red;
		if (isWarning)
			Console.ForegroundColor = ConsoleColor.DarkYellow;
		Console.WriteLine(content);
		if (isError || isWarning)
			Console.ResetColor();
		System.IO.File.AppendAllText(LogFile.FullName, content + "\r\n");
	}

	/// <summary>
	/// Generate C# enum objects from database tables.
	/// </summary>
	public static void GenerateEnums(string schemaName = "Types") {
		Log("GenerateEnums: START");
		UpdateEnvironmnet();
		UpdateDataFromDatabase();
		// ----------------------------------------------------------------------------
		if (!_SchemaNames.Contains(schemaName)) {
			Log(Warning + ": Schema {0} not found!", schemaName);
			return;
		}
		var di = new DirectoryInfo(GetPath(schemaName));
		var oldFiles = di.Exists ? di.GetFiles("*.cs").Select(x => x.FullName).ToList() : new List<string>();
		var newFiles = new List<string>();
		var tableNames = GetTableNames(schemaName);
		// For each table...
		for (var t = 0; t < tableNames.Count; t++) {
			var tableName = tableNames[t];
			// Select data.
			bool isFlags;
			var table = GetEnumData(schemaName, tableName, out isFlags);
			// -----CONTENT START-----
			var content = "";
			if (isFlags)
				content += "using System;\r\n";
			content += "using System.ComponentModel;\r\n";
			content += "\r\n";
			content += "namespace " + config.Namespace + "\r\n";
			content += "{\r\n";
			// -----NAMESPACE CONTENT START-----
			var nsContent = "";
			if (isFlags)
				nsContent += "[Flags]\r\n";
			nsContent += "public enum " + tableName + "\r\n";
			nsContent += "{\r\n";
			// -----CLASS CONTENT START-----
			nsContent += IdentText(GetEnumConent(table, isFlags), 1);
			// -----CLASS CONTENT END-----
			nsContent += "}\r\n";
			// -----NAMESPACE CONTENT END-----
			content += IdentText(nsContent, 1);
			content += "}\r\n";
			// -----CONTENT END-----
			var path = di.FullName + "\\" + tableName + ".cs";
			Log("  {0}\\{1}.cs", di.Name, tableName);
			if (!di.Exists)
				di.Create();
			newFiles.Add(path);
			WriteIfDifferent(path, content);
		}
		Cleanup(oldFiles, newFiles);
		// Get old file paths which do not exist in the new list.
		Log("GenerateEnums: END");
	}

	/// <summary>
	/// Get information about columns. Use data by executing SQL script from *.sql file or from [Security].[ColumnInfo] table.
	/// </summary>
	public static DataTable GetColumnInfo(bool useColumnInfoTable = false) {
		var sql = "SELECT * FROM [Security].[ColumnInfo]";
		if (!useColumnInfoTable) {
			sql = File.ReadAllText(TemplateFile.FullName + ".sql");
			// Remove CREATE PROCEDURE block.
			sql = sql.Substring(sql.IndexOf("AS") + 2);
		}
		var cmd = new SqlCommand(sql);
		cmd.Parameters.AddWithValue("@SchemaName", DBNull.Value);
		cmd.Parameters.AddWithValue("@TableName", DBNull.Value);
		cmd.Parameters.AddWithValue("@Name", DBNull.Value);
		cmd.Parameters.AddWithValue("@IsMsShipped", false);
		cmd.CommandType = CommandType.Text;
		var table = ExecuteDataTable(cmd);
		return table;
	}

	/// <summary>
	/// Check if specific object exists in database.
	/// </summary>
	public static bool DbContains(string name, string type = null) {
		var sql = "SELECT * FROM sys.objects WHERE name = @name AND (@type IS NULL OR [type] = @type)";
		var cmd = new SqlCommand(sql);
		cmd.Parameters.AddWithValue("@name", name);
		cmd.Parameters.AddWithValue("@type", type == null ? (object)DBNull.Value : type);
		cmd.CommandType = CommandType.Text;
		var table = ExecuteDataTable(cmd);
		return table.Rows.Count > 0;
	}

	private static DataTable GetEnumData(string schemaName, string tableName, out bool isFlags) {
		var cmd = new SqlCommand("SELECT * FROM [" + schemaName + "].[" + tableName + "]");
		cmd.CommandType = CommandType.Text;
		var table = ExecuteDataTable(cmd);
		// Enumeration is not flags by default.
		isFlags = false;
		if (!table.Columns.Contains("id"))
			return table;
		var values = table.Rows.Cast<DataRow>().Select(x => (int)x["id"]).OrderBy(x => x);
		var valueLine = string.Join(",", values);
		var rx = new Regex("\\b1,2,4\\b");
		// Is [Flags] enum if contains sequence: 1, 2, 4.
		isFlags = rx.IsMatch(valueLine);
		Log($"IsFlags: {isFlags} - {valueLine}");
		return table;
	}

	private static string GetEnumConent(DataTable table, bool isFlags) {
		// Add enumeration properties.
		var content = "";
		for (var r = 0; r < table.Rows.Count; r++) {
			var id = "";
			var value = "";
			var description = "";
			var summary = "";
			var row = table.Rows[r];
			int? iId;
			if (table.Columns.Contains("id")) {
				iId = (int)row["id"];
				id = iId.Value.ToString();
				var iStart = isFlags ? 0 : 8;
				for (int i = iStart; i < 32; i++) {
					int pow;
					var upow = (uint)Math.Pow(2, i);
					pow = unchecked((int)upow);
					if (iId == pow)
						id = "1 << " + i;
				}
				if (id == "" && iId > byte.MaxValue && iId < int.MaxValue)
					id = "0x" + iId.Value.ToString("X2");
			}
			if (table.Columns.Contains("value"))
				value = (string)row["value"];
			if (table.Columns.Contains("description"))
				description = (string)row["description"];
			if (table.Columns.Contains("summary"))
				summary = (string)row["summary"];
			if (summary != "" && summary != null)
				content += GetSummary(summary) + "\r\n";
			if ((description != "" || value == "None") && description != null)
				content += "[Description(\"" + description + "\")]\r\n";
			//if (iId.HasValue)
			//	content += "\t\t[EnumMember(Value = \""+iId.Value.ToString()+"\")]\r\n";
			content += value;
			if ((id != "" || value == "None") && id != null)
				content += " = " + id;
			content += ",\r\n";
		}
		return content;
	}

	private static void FillAssociations(
		string schemaName, string tableName,
		List<DataRow> columnRows,
		List<ClassDiagramClassProperty> showAsAssociation,
		out string contents
	) {
		contents = "";
		// Foreign Primary Key Items/Lists.
		var fkItems = "";
		var fkLists = "";
		for (var c = 0; c < columnRows.Count; c++) {
			var row = columnRows[c];
			var name = (string)row["Name"];
			if (config.GenerateOneToManyItems)
				fkItems += GetOneToManyItems(schemaName, tableName, name, showAsAssociation);
			if (config.GenerateOneToManyLists)
				fkLists += GetOneToManyLists(schemaName, tableName, name);
		}
		if (!string.IsNullOrEmpty(fkItems))
			contents += IdentText("\r\n#region Foreign Key Items\r\n" + fkItems + "\r\n#endregion\r\n", 2);
		if (!string.IsNullOrEmpty(fkLists))
			contents += IdentText("\r\n#region Foreign Key Lists\r\n" + fkLists + "\r\n#endregion\r\n", 2);
	}

	private static void GenerateClassProperties(
			GenType genType,
			GenItemType genItemType,
			string schemaName, string tableName,
			List<DataRow> columnRows,
			List<string> requiredNamespaces,
			out string contents
			) {
		contents = "";
		var containsExternal = columnRows.Any(x => useExternalCustomOption(x));
		var requiredNsEntityFrameworkCore = false;
		var requiredNsDataAnnotationsSchema = false;
		for (var c = 0; c < columnRows.Count; c++) {
			var row = columnRows[c];
			// If no records are marked as external then everything is external.
			var isExternal = useExternalCustomOption(row) || !containsExternal;
			// If generating external content and item is internal or generating internal content and item is external.
			if ((genItemType == GenItemType.ExternalClass && !isExternal) || (genItemType == GenItemType.InternalClass && isExternal))
				continue;
			var Id = (string)row["Id"];
			var Index = (int)row["Index"];
			var type = (string)row["Type"];
			var name = (string)row["Name"];
			var Length = (int)row["Length"];
			var Precision = (int)row["Precision"];
			var Scale = (int)row["Scale"];
			var Default = (string)row["Default"];
			var IsCustomLength = (bool)row["IsCustomLength"];
			var IsCustomPrecision = (bool)row["IsCustomPrecision"];
			var IsPrimaryKey = (bool)row["IsPrimaryKey"];
			var IsIdentity = (bool)row["IsIdentity"];
			var IsMsShipped = (bool)row["IsMsShipped"];
			var IsNullable = (bool)row["IsNullable"];
			var Collation = (string)row["Collation"];
			var description = (string)row["Description"];

			var sqlType = (SqlDataType)Enum.Parse(typeof(SqlDataType), type, true);
			var sysType = ToSystemType(sqlType);
			var columnPropertyType = GetColumnPropertyType(row);
			var columnPropertyName = GetColumnPropertyName(row);

			//------------------------------------------------------
			// Create C# code.
			var propSummary = string.IsNullOrEmpty(description)
				? string.Format(config.DefaultColumnSummary, ConvertNameToString(name)) + (IsPrimaryKey ? " (unique primary key)." : "")
				: description;
			propSummary = GetSummary(propSummary);
			var csProp = "\r\n";
			csProp += propSummary + "\r\n";
			if (genItemType == GenItemType.InternalClass || genItemType == GenItemType.ExternalClass) {
				// Add primary key attribute.
				if (IsPrimaryKey)
					csProp += "[Key]\r\n";
				// Add identity attribute.
				if (IsIdentity)
					csProp += "[DatabaseGenerated(DatabaseGeneratedOption.Identity)]\r\n";
				//if (!isExternal)
				//	csProp +=  "[Internal]\r\n";
				if (type.Contains("decimal") && IsCustomPrecision)
					//csProp += $"[Column(TypeName = \"decimal({Precision}, {Scale})\")]\r\n";
					csProp += $"[Precision({Precision}, {Scale})]\r\n";
				if ((type.Contains("char") || type.Contains("text")) && !type.StartsWith("n") && config.EnableEntityFrameworkCore && !config.EnableEntityFramework6) {
					requiredNsEntityFrameworkCore = true;
					csProp += $"[Unicode(false)]\r\n";
				}
				// If nullable reference types are not enabled then EF don't know if string is required.
				if (!config.EnableNullableReferenceTypes && Type.GetTypeCode(sysType) == TypeCode.String && !IsNullable)
					csProp += $"[Required]\r\n";
			}
			// Validation attributes which work on EF and API.
			if (useStringLengthAttribute(row))
				csProp += "[StringLength(" + Length + ")]\r\n";
			var csPublic = genType == GenType.Class
				? "public "
				: "";

			var isClassDateTime =
				genType == GenType.Class &&
				Type.GetTypeCode(sysType) == TypeCode.DateTime;

			if (config.UseDateTimeKindUtcAttribute && isClassDateTime)
				csProp += "[DateTimeKind(DateTimeKind.Utc)]\r\n";

			var oRow = config.OverrideColumnInfo.Rows.Cast<DataRow>().FirstOrDefault(x =>
				$"{schemaName}.{tableName}.{name}".Equals((string)x["Id"], StringComparison.Ordinal)
			);

			if (config.UseDateTimeKindUtcProperty && isClassDateTime) {
				csProp += "" + csPublic + columnPropertyType + " " + columnPropertyName + " {\r\n";
				// Solution 1 - Set all to UTC.
				//csProp += "\tget => DateTime.SpecifyKind(_" + name + ", DateTimeKind.Utc);\r\n";
				//csProp += "\tset => _" + name + " = DateTime.SpecifyKind(value, DateTimeKind.Utc);\r\n";
				// Solution 2 - Treat unknown datetimes types as UTC, convert others to UTC.
				//csProp += "\tget => value.Kind == DateTimeKind.Unspecified\r\n";
				//csProp += "? DateTime.SpecifyKind(value, DateTimeKind.Utc)\r\n";
				//csProp += ": value.ToUniversalTime();\r\n";
				//csProp += "\tset => _" + name + " = value.Kind == DateTimeKind.Unspecified\r\n";
				//csProp += "? DateTime.SpecifyKind(value, DateTimeKind.Utc)\r\n";
				//csProp += ": value.ToUniversalTime();\r\n";
				// Solution 3 - Use DateTimeHelper class.
				csProp += "\tget => DateTimeHelper.ConvertToUtc(_" + name + ");\r\n";
				csProp += "\tset => _" + name + " = DateTimeHelper.ConvertToUtc(value);\r\n";
				// Close property.
				csProp += "}\r\n";
				csProp += "private " + columnPropertyType + " _" + name + ";\r\n";
			}
			else if (config.StoreEnumDataAsStrings && oRow != null && (string)oRow["IsEnum"] == "1") {
				var enumType = oRow["CustomType"];
				csProp += "" + csPublic + columnPropertyType + " " + columnPropertyName + " { get; set; }\r\n";
				csProp += "\r\n";
				csProp += "" + GetSummary("") + "\r\n";
				csProp += "[NotMapped]\r\n";
				csProp += "" + csPublic + enumType + " " + columnPropertyName.Replace("String", "") + " {\r\n";
				csProp += "\tget => Enum.Parse<" + enumType + ">(" + columnPropertyName + ");\r\n";
				csProp += "\tset => " + columnPropertyName + " = value.ToString();\r\n";
				csProp += "}\r\n";
				requiredNsDataAnnotationsSchema = true;
			}
			else {
				csProp += "" + csPublic + columnPropertyType + " " + columnPropertyName + " { get; set; }\r\n";
			}
			contents += csProp;
		}
		if (requiredNsEntityFrameworkCore)
			requiredNamespaces?.Add("Microsoft.EntityFrameworkCore");
		if (requiredNsDataAnnotationsSchema)
			requiredNamespaces?.Add("System.ComponentModel.DataAnnotations.Schema");
	}

	/// <summary>
	/// Set to true if generation script finds [Tools_GetColumnInfo] stored procedure in database.
	/// </summary>
	private static bool _useColumnInfoTable;

	private static List<DataRow> _columnInfoRows;

	private static string _ModelClassName;

	private static List<string> _SchemaNames;

	private static void UpdateEnvironmnet() {
		var scriptPath = TemplateFile.Directory.FullName;
		_ModelClassName = string.IsNullOrEmpty(config.ModelClassName)
				? System.IO.Path.GetFileNameWithoutExtension(TemplateFile.FullName)
				: config.ModelClassName;
		// Change current directory.
		Log("Path: {0}", scriptPath);
		Environment.CurrentDirectory = scriptPath;
	}

	private static void UpdateDataFromDatabase() {
		_useColumnInfoTable = DbContains("Tools_GetColumnInfo") && DbContains("Tools_UpdateColumnInfo");
		Log("UseColumnInfoTable = {0}", _useColumnInfoTable);
		// If stored procedure, which updates column info was found then...
		if (_useColumnInfoTable) {
			// Update [Security].[ColumnInfo] table and apply changes.
			var updateSql = "EXEC [Security].[Tools_UpdateColumnInfo] 1, 1, 1, 1, 1";
			var updateCmd = new SqlCommand(updateSql);
			updateCmd.CommandType = CommandType.Text;
			_ = ExecuteDataTable(updateCmd);
		}
		// Get all columns.
		_columnInfoRows = GetColumnInfo(_useColumnInfoTable).Rows
			.Cast<DataRow>()
			// Select all rows is using script from SQL file.
			// Select schemas with approved (8) columns.
			.Where(x => !_useColumnInfoTable || ((int)x["CustomOptions"] & 8) == 8)
			.ToList();
		// Get all schema names.
		_SchemaNames = _columnInfoRows
			.OrderBy(x => (string)x["SchemaName"])
			.Select(x => (string)x["SchemaName"])
			.Distinct()
			.ToList();
		// Log stats.
		Log("Schema Count: {0}", _SchemaNames.Count);
	}

	/// <summary>
	/// Generate C# classes from database tables.
	/// </summary>
	public static void GenerateClasses(
			GenType genType,
			bool inclusive = true,
			params string[] schemaFilter
		) {
		Log("GenerateClasses: START");
		UpdateEnvironmnet();
		UpdateDataFromDatabase();
		var classDiagram = new ClassDiagram() {
			MajorVersion = 1,
			MinorVersion = 1,
			Class = new List<ClassDiagramClass>(),
		};

		var generateClasses = genType == GenType.Class;
		// ----------------------------------------------------------------------------
		var schemaNames = _SchemaNames
			// If exclusive then skip inclusive filter.
			.Where(x => schemaFilter == null || !inclusive || schemaFilter.Contains(x))
			// If inclusive then skip exclusive filter.
			.Where(x => schemaFilter == null || inclusive || !schemaFilter.Contains(x))
			.ToList();
		//------------------------------------------------------
		// Create C# code.
		//------------------------------------------------------
		// Using data anotations.
		var usingDA = "";
		usingDA += "using System;\r\n";
		usingDA += "using System.Collections.Generic;\r\n";
		usingDA += "using System.CodeDom.Compiler;\r\n";
		// Support for API (ASP.NET MVC and ASP.NET) attributes.
		usingDA += "using System.ComponentModel.DataAnnotations;\r\n";
		usingDA += "using System.ComponentModel.DataAnnotations.Schema;\r\n";

		if (config.ContextNamespace != config.Namespace)
			usingDA += $"using {config.Namespace};\r\n";

		// Using entity framework.
		var usingEF = GetEntityUsings();

		var nsOpen = "\r\nnamespace " + config.Namespace + "\r\n{\r\n\r\n";
		var aClose = "";
		aClose += "\r\n";
		aClose += "\t}\r\n";
		aClose += "}\r\n";
		// Context External
		var tmp = "";
		var contextE = "";
		var contextI = "";
		if (generateClasses) {
			var nsOpenContext = usingDA + usingEF + "\r\n" + "namespace " + config.ContextNamespace + "\r\n{\r\n";
			var classSummary = GetSummary(ConvertNameToString(_ModelClassName));
			var initializeSummary = GetSummary("Initialize model");
			contextI += nsOpenContext + IdentText(classSummary) + "\r\n";
			contextI += "\tpublic partial class " + _ModelClassName + "\r\n";
			contextI += "\t{\r\n";
			contextI += "\r\n";
			// External contect contains initializer.
			contextE += nsOpenContext + IdentText(classSummary) + "\r\n";
			if (config.AddGeneratedCodeAttribute)
				contextE += $"\t[GeneratedCode(\"{nameof(MainModel)}\", \"{GeneratorVersion}\")]\r\n";
			contextE += "\tpublic partial class " + _ModelClassName + " : DbContext\r\n";
			contextE += "\t{\r\n\r\n";
			contextE += "\r\n";
			contextE += IdentText(initializeSummary, 2) + "\r\n";
			contextE += "\t\tpublic " + _ModelClassName + "() : base() { }\r\n";
			contextE += "\r\n";
			contextE += "#if NETFRAMEWORK\r\n";
			contextE += "#else\r\n";

			// Add context NET 6.0 help.
			contextE += "\t\t/*\r\n";
			contextE += "\r\n";
			contextE += "\t\t// Set connection string for " + _ModelClassName + " before calling \"var app = builder.Build(); \" in Program.cs\r\n";
			contextE += "\t\t// Requires: using Microsoft.EntityFrameworkCore;\r\n";
			contextE += "\t\tbuilder.Services.AddDbContext<" + _ModelClassName + ">(x =>\r\n";
			contextE += "\t\t{\r\n";
			contextE += "\t\t\tvar connectionString = builder.Configuration.GetConnectionString(nameof(" + _ModelClassName + ") + \"Connection\");\r\n";
			contextE += "\t\t\tx.UseSqlServer(connectionString);\r\n";
			contextE += "\t\t});\r\n";
			contextE += "\t\tvar db = " + _ModelClassName + ".Create(connectionString);\r\n";
			contextE += "\r\n";
			contextE += "\t\t// How to initialize context and retrieve options.\r\n";
			contextE += "\t\tvar options = app.Services.GetService<DbContextOptions<" + _ModelClassName + ">>();\r\n";
			contextE += "\t\tvar db = new " + _ModelClassName + "(options)();\r\n";
			contextE += "\r\n";
			contextE += "\t\tvar db = app.Services.GetService<" + _ModelClassName + ">();\r\n";
			contextE += "\r\n";
			contextE += "\t\t*/\r\n";
			contextE += "\r\n";

			// Add initializer with options.
			contextE += IdentText(GetSummary("Initialize model with options."), 2) + "\r\n";
			contextE += "\t\tpublic " + _ModelClassName + "(DbContextOptions<" + _ModelClassName + "> options) : base(options) { }\r\n";
			// Add static create with connection string method. 
			contextE += "\r\n";
			contextE += IdentText(GetSummary("Create context with connection string."), 2) + "\r\n";
			contextE += "\t\tpublic static " + _ModelClassName + " Create(string connectionString) {\r\n";
			contextE += "\t\t	var optionsBuilder = new DbContextOptionsBuilder<" + _ModelClassName + ">();\r\n";
			contextE += "\t\t	optionsBuilder.UseSqlServer(connectionString);\r\n";
			contextE += "\t\t	return new " + _ModelClassName + "(optionsBuilder.Options);\r\n";
			contextE += "\t\t}\r\n";
			contextE += "\r\n";
			contextE += "\t\tprotected override void OnModelCreating(ModelBuilder modelBuilder) {\r\n";
			contextE += "\t\t\tmodelBuilder.Ignore<Type>();\r\n";
			contextE += "\t\t\tmodelBuilder.Ignore<System.Reflection.CustomAttributeData>();\r\n";
			contextE += "\t\t\tbase.OnModelCreating(modelBuilder);\r\n";
			contextE += "\t\t}\r\n";
			contextE += "\r\n";
			contextE += "#endif\r\n";
			contextE += "\r\n";
		}

		//------------------------------------------------------
		// For each Schema...
		for (var s = 0; s < schemaNames.Count; s++) {
			var schemaName = schemaNames[s];
			// Get old files.
			var schemaDir = new DirectoryInfo(GetPath(schemaName));
			// List of old files that should be deleted.
			var oldFiles = new List<string>();
			// List of new files created.
			var newFiles = new List<string>();
			if (schemaDir.Exists)
				oldFiles.AddRange(schemaDir.GetFiles("*.cs").Select(x => x.FullName));
			var tableNames = GetTableNames(schemaName);
			Log("  [{0}] Schema: {1} Table{2}", schemaName,
				tableNames.Count,
				tableNames.Count == 1 ? "" : "s");

			// For each Table...
			for (var t = 0; t < tableNames.Count; t++) {
				var tableName = tableNames[t];
				var tableItemType = GetTableItemType(schemaName, tableName);
				var tablePropertyName = GetTablePropertyName(tableItemType);
				var inheritancePV = GetEntityInheritance(schemaName, tableName);
				Log("    {0}", tableName);
				//------------------------------------------------------
				var columnRows = FilterRows(schemaName, tableName);
				//------------------------------------------------------
				// Check primary keys.
				var pkeys = 0;
				for (var c = 0; c < columnRows.Count; c++) {
					var row = columnRows[c];
					if ((bool)row["IsPrimaryKey"]) {
						pkeys += 1;
					}
				}
				if (genType == GenType.Class && pkeys == 0) {
					Log("      " + Warning + ": No Primary keys: {0}.{1}", schemaName, tableName);
					//continue;
				}
				if (pkeys > 1) {
					Log("      " + Warning + ": Multiple Primary keys: {0}.{1}", schemaName, tableName);
					//continue;
				}
				//------------------------------------------------------
				var nsSchemaOpen = "";
				var nsSchemaClose = "";
				var nsClassPrefix = "";
				var nsNamePrefix = "";
				if (schemaName != config.ContextDefaultSchema || schemaName != "") {
					//nsSchemaOpen += "\tnamespace schemaName\r\n";
					//nsSchemaOpen += "\t{\r\n";
					//nsSchemaOpen += "\r\n";
					//nsSchemaClose += "\r\n";
					//nsSchemaClose += "\t}\r\n";
					//nsSchemaClose += "\r\n";
					//nsClassPrefix = "schemaName.";
					//nsNamePrefix = "schemaName";
				}
				var csObjectType = genType.ToString().ToLower();

				var tableSummary = GetSummary(string.Format(config.DefaultTableSummary, ConvertNameToString(tableName)));
				// External attributes.
				var attE = "";
				if (config.AddGeneratedCodeAttribute)
					attE += $"\t[GeneratedCode(\"{nameof(MainModel)}\", \"{GeneratorVersion}\")]\r\n";
				if (genType == GenType.Class && (schemaName != config.ContextDefaultSchema || tableName != tableItemType))
					attE += "\t[Table(\"" + tableName + "\", Schema = \"" + schemaName + "\")]\r\n";
				tmp = "\tpublic partial " + csObjectType + " " + tableItemType;
				if (!string.IsNullOrEmpty(inheritancePV)) {
					tmp += ": " + inheritancePV;
				}
				tmp += "\r\n";
				tmp += "\t{\r\n";
				// If any of the columns have custom precision then [Precision] attribute will be used which requires EntityCore.
				var internalText = usingDA;
				var externalText = usingDA;
				var transferText = usingDA;
				if (columnRows.Any(x => (bool)x["IsCustomPrecision"])) {
					internalText += usingEF;
					externalText += usingEF;
				}
				// Data Item class.
				internalText += nsOpen + nsSchemaOpen + IdentText(tableSummary, 1) + "\r\n" + tmp;
				externalText += nsOpen + nsSchemaOpen + IdentText(tableSummary, 1) + "\r\n" + attE + tmp;
				//------------------------------------------------------
				// 3.5 = 350px 
				var width = 0.5m + Math.Ceiling(tableItemType.Length * 0.075m / 0.25m) * 0.25m;
				var classDiagramClass = new ClassDiagramClass() {
					Name = config.Namespace + "." + tableItemType,
					Collapsed = true,
					Position = new ClassDiagramClassPosition() { X = 0.5m, Y = 0.5m, Width = width },
					TypeIdentifier = new ClassDiagramClassTypeIdentifier() {
						//HashCode =
						FileName = schemaName + "\\" + tableItemType + ".cs",
					},
					ShowAsAssociation = new List<ClassDiagramClassProperty>(),
				};
				classDiagram.Class.Add(classDiagramClass);
				//------------------------------------------------------
				// For each Column...
				string foreignKeyContent;
				FillAssociations(schemaName, tableName, columnRows, classDiagramClass.ShowAsAssociation, out foreignKeyContent);
				string internalProperties = "";
				GenerateClassProperties(genType, GenItemType.InternalClass, schemaName, tableName, columnRows, null, out internalProperties);
				var internalWrite = !string.IsNullOrEmpty(internalProperties);
				string externalProperties = "";
				GenerateClassProperties(genType, GenItemType.ExternalClass, schemaName, tableName, columnRows, null, out externalProperties);
				var externalWrite = !string.IsNullOrEmpty(externalProperties);
				if (classDiagramClass.ShowAsAssociation.Count == 0)
					classDiagramClass.ShowAsAssociation = null;
				// Create C# code for table inside context.
				tmp = "\r\n";
				tmp += IdentText(GetSummary(ConvertNameToString(tableItemType)), 2) + "\r\n";
				tmp += "\t\tpublic virtual DbSet<" + nsClassPrefix + tableItemType + "> " + nsNamePrefix + tablePropertyName + " { get; set; }\r\n";
				if (externalWrite) {
					if (!schemaDir.Exists)
						schemaDir.Create();
					externalText += IdentText(externalProperties, 2);
					externalText += foreignKeyContent;
					if (config.GenerateCloneAndCopyMethods && genType == GenType.Class) {
						externalText += "\r\n";
						externalText += IdentText(GenerateCloneAndCopyMethods(nsClassPrefix + tableItemType, columnRows, ""), 2);
						externalText += "\r\n";
					}
					externalText += nsSchemaClose + aClose;
					var externalPath = schemaDir.FullName + "\\" + tableItemType + ".cs";
					newFiles.Add(externalPath);
					WriteIfDifferent(externalPath, externalText);
					if (generateClasses)
						contextE += tmp;
				}
				else if (internalWrite) {
					if (generateClasses)
						contextI += tmp;
				}
				if (internalWrite) {
					if (!schemaDir.Exists)
						schemaDir.Create();
					internalText += IdentText(internalProperties, 2);
					internalText += nsSchemaClose + aClose;
					var internalPath = schemaDir.FullName + "\\" + tableItemType + ".Internal.cs";
					newFiles.Add(internalPath);
					WriteIfDifferent(internalPath, internalText);
				}
			}
			// Get old file paths which do not exist in the new list.
			var filesToDelete = oldFiles.Except(newFiles).ToList();
			for (int i = 0; i < filesToDelete.Count; i++)
				File.Delete(filesToDelete[i]);
		}
		if (generateClasses) {
			if (config.GenerateControllers)
				GenerateControllers(schemaNames);
			//------------------------------------------------------
			// Create C# code.
			contextE += aClose;
			contextI += aClose;
			//------------------------------------------------------
			if (config.GenerateContext) {
				// Write external context.
				var contextPathE = GetPath($"{config.ModelClassName}.cs");
				Log("  {0}", contextPathE);
				WriteIfDifferent(contextPathE, contextE);
				// Write internal context.
				var contextPathI = GetPath($"{config.ModelClassName}.Internal.cs");
				Log("  {0}", contextPathI);
				WriteIfDifferent(contextPathI, contextI);
			}

			// Split classes into levels.
			var remaining = classDiagram.Class.ToArray();
			var levels = new List<ClassDiagramClass[]>();
			// Get items with no associations.
			var noAssocItems = remaining.Where(x => x.ShowAsAssociation?.Any() != true).ToArray();
			// Get all types of associations.
			var assocTypes = remaining
				.Where(x => x.ShowAsAssociation != null)
				.SelectMany(x => x.ShowAsAssociation.Select(a => a.Type)).Distinct().ToArray();
			noAssocItems = noAssocItems.Where(x => !assocTypes.Contains(x.Name.Split('.').Last())).ToArray();
			levels.Add(noAssocItems);
			Log($"\tClass Diagram Level {levels.Count}: {levels[levels.Count - 1].Length} item(s)");
			remaining = remaining.Except(noAssocItems).ToArray();
			// Slit into linked levels.
			var children = new ClassDiagramClass[0];
			while (true) {
				GetChildren(children, remaining, out children, out remaining);
				if (children.Length == 0)
					break;
				levels.Add(children);
				Log($"\tClass Diagram Level {levels.Count}: {levels[levels.Count - 1].Length} item(s)");
			}
			if (remaining.Length > 0) {
				levels.Add(remaining);
				Log($"\tClass Diagram Level {levels.Count}: {levels[levels.Count - 1].Length} item(s)");
			}
			// Reorder class diagram.
			for (int l = 0; l < levels.Count; l++) {
				var level = levels[l];
				var x = 0.5m;
				for (int c = 0; c < level.Length; c++) {
					var classItem = level[c];
					classItem.Position.Y = l + 0.5m;
					classItem.Position.X = x;
					x += classItem.Position.Width + 0.25m;
				}
			}
			// Serialize class diagram.
			var cdContent = SeriallizeToXmlString(classDiagram, null, true);
			var contentPathCD = GetPath($"{config.ModelClassName}.cd");
			Log("  {0}", contentPathCD);
			WriteIfDifferent(contentPathCD, cdContent);
		}
		Log("GenerateClasses: END");
	}

	/// <summary>Cleanup files.</summary>
	private static void Cleanup(List<string> oldFiles, List<string> newFiles) {
		// Get old file paths which do not exist in the new list.
		var filesToDelete = oldFiles.Where(x => !newFiles.Any(y => x.Equals(y, StringComparison.OrdinalIgnoreCase))).ToList();
		for (int i = 0; i < filesToDelete.Count; i++) {
			Log("Warning: Delete File " + filesToDelete[i]);
			File.Delete(filesToDelete[i]);
		}
	}

	#region Generate: Controllers


	public static void GenerateControllers(List<string> schemaNames) {
		Log("  Controllers:");
		var di = new DirectoryInfo(GetPath("Controllers"));
		var oldFiles = di.Exists ? di.GetFiles("*.cs").Select(x => x.FullName).ToList() : new List<string>();
		var newFiles = new List<string>();
		for (var s = 0; s < schemaNames.Count; s++) {
			var schemaName = schemaNames[s];
			var tableNames = GetTableNames(schemaName);
			// For each Table...
			for (var t = 0; t < tableNames.Count; t++) {
				var tableName = tableNames[t];
				var tableItemType = GetTableItemType(schemaName, tableName);
				var columnRows = FilterRows(schemaName, tableName);
				string content;
				if (GetControllerFileContent(schemaName, tableName, config.ModelClassName, tableItemType, columnRows, out content)) {
					if (!di.Exists)
						di.Create();
					var fi = new FileInfo(di.FullName + "\\" + tableItemType + "Controller.cs");
					Log($"    {fi.Name}");
					newFiles.Add(fi.FullName);
					WriteIfDifferent(fi.FullName, content);
				}
			}
		}
		Cleanup(oldFiles, newFiles);
	}

	public static bool GetControllerFileContent(
		string schemaName,
		string tableName,
		string modelClassName,
		string tableModelName,
		List<DataRow> columnRows,
		out string contents
	) {
		contents = "";
		var primaryKeyRow = columnRows.FirstOrDefault(x => (bool)x["IsPrimaryKey"]);
		if (primaryKeyRow == null) {
			Log($"      Error: No primary keys!");
			return false;
		}
		var tablePropertyName = GetTablePropertyName(tableModelName);
		var modelPropertyName = ToCamelCase(modelClassName);
		var primaryColumnKeyType = GetColumnPropertyType(primaryKeyRow);
		var primaryColumnKeyName = GetColumnPropertyName(primaryKeyRow);
		var idName = ToCamelCase(primaryColumnKeyName);
		var paramName = "item";
		var prefixedParamName = "Item";
		var dtoSuffix = config.GenerateDTO ? "Dto" : "";
		// Generate Data Transfer Object (DTO).
		string transferClass = "";
		var requiredNamespaces = new List<string>();
		if (config.GenerateDTO) {
			transferClass += "public partial class " + tableModelName + "Dto {\r\n";
			string tansferProperties = "";
			GenerateClassProperties(GenType.Class, GenItemType.TransferClass, schemaName, tableName, columnRows, requiredNamespaces, out tansferProperties);
			transferClass += IdentText(tansferProperties, 1) + "\r\n";
			transferClass += "}";
		}
		// -----CONTENT START-----
		contents += "using System;\r\n";
		if (config.AddGeneratedCodeAttribute)
			contents += "using System.CodeDom.Compiler;\r\n";
		contents += "using System.Threading;\r\n";
		contents += "using System.Threading.Tasks;\r\n";
		contents += "using Microsoft.AspNetCore.Mvc;\r\n";
		foreach (var requiredNamespace in requiredNamespaces)
			contents += $"using {requiredNamespace};\r\n";
		// Use foreigh key lists.
		contents += "using System.Collections.Generic;\r\n";
		if (config.GenerateDTO && columnRows.Any(x => useStringLengthAttribute(x)))
			contents += "using System.ComponentModel.DataAnnotations;\r\n";
		if (config.GenerateDTO) {
			contents += "using System.Linq;\r\n"; ;
			contents += GetEntityUsings();
		}
		if (config.ControllerNamespace != config.Namespace)
			contents += $"using {config.Namespace};\r\n";
		if (config.ControllerNamespace != config.ContextNamespace)
			contents += $"using {config.ContextNamespace};\r\n";
		contents += "\r\n";
		contents += "namespace " + config.ControllerNamespace + "\r\n";
		contents += "{\r\n";
		var copyProperties = "";
		copyProperties += "\r\n";
		copyProperties += "#region Copy Properties\r\n";
		copyProperties += "\r\n";
		if (config.GenerateDTO) {
			// ----- Create Convert functions.
			// -- From DTO...
			copyProperties += $"private {tableModelName} CopyProperties({tableModelName}{dtoSuffix} source, {tableModelName} target, bool copyKey = false) {{\r\n";
			foreach (var row in columnRows) {
				var columnPropertyName = GetColumnPropertyName(row);
				if ((bool)row["IsPrimaryKey"])
					copyProperties += $"\tif (copyKey)\r\n\t";
				copyProperties += $"\ttarget.{columnPropertyName} = source.{columnPropertyName};\r\n";
			}
			copyProperties += "\treturn target;\r\n";
			copyProperties += $"}}";
			// -- To DTO...
			copyProperties += "\r\n";
			copyProperties += "\r\n";
			copyProperties += $"private {tableModelName}{dtoSuffix} CopyProperties({tableModelName} source, {tableModelName}{dtoSuffix} target, bool copyKey = false) {{\r\n";
			foreach (var row in columnRows) {
				var columnPropertyName = GetColumnPropertyName(row);
				if ((bool)row["IsPrimaryKey"])
					copyProperties += $"\tif (copyKey)\r\n\t";
				copyProperties += $"\ttarget.{columnPropertyName} = source.{columnPropertyName};\r\n";
			}
			copyProperties += "\treturn target;\r\n";
			copyProperties += $"}}";
		}
		copyProperties += "\r\n";
		copyProperties += "\r\n";
		copyProperties += "#endregion";

		var returnItemType = config.ControllerUseActionResult
			? $"ActionResult<{tableModelName}{dtoSuffix}>"
			: $"{tableModelName}{dtoSuffix}";

		var returnListType = config.ControllerUseActionResult
			? $"ActionResult<List<{tableModelName}{dtoSuffix}>>"
			: $"List<{tableModelName}{dtoSuffix}>";

		// Return type.
		var returnLine = config.GenerateDTO
			? $"\t\treturn CopyProperties(db{prefixedParamName}, new {tableModelName}{dtoSuffix}(), true);\r\n"
			: $"\t\treturn db{prefixedParamName};\r\n";

		if (config.ControllerUseActionResult)
			returnLine = new Regex("return (?<obj>.*);").Replace(returnLine, "return Ok(${obj});");

		// -----NAMESPACE CONTENT START-----
		var controllerBaseClassList = string.IsNullOrEmpty(config.ControllerBaseClassList) ? "ControllerBase" : config.ControllerBaseClassList;
		var dataAccessClassName = $"{tableModelName}DataAccess";
		var dataAccessPropertyName = ToCamelCase(dataAccessClassName);
		// Create class content.
		var initializerProperties = new List<string>();
		initializerProperties.Add($"{modelClassName} {modelPropertyName}");
		if (config.GenerateDataAccessClasses)
			initializerProperties.Add($"{dataAccessClassName} {dataAccessPropertyName}");
		var s = "";
		s += GetSummary($"API Controller for {ConvertNameToString(tableModelName)} record.") + "\r\n";
		s += config.AddGeneratedCodeAttribute ? $"[GeneratedCode(\"{nameof(MainModel)}\", \"{GeneratorVersion}\")]\r\n" : "";
		s += $"[ApiController, Route(\"api/[controller]\")]\r\n";
		s += $"public partial class {tableModelName}Controller: {controllerBaseClassList} {{\r\n";
		s += $"\r\n";
		s += $"\tprivate readonly {modelClassName} {modelPropertyName};\r\n";
		if (config.GenerateDataAccessClasses)
			s += $"\tprivate readonly {dataAccessClassName} {dataAccessPropertyName};\r\n";
		s += $"\r\n";
		s += $"\tpublic {tableModelName}Controller({string.Join(", ", initializerProperties)}) {{\r\n";
		s += $"\t\tthis.{modelPropertyName} = {modelPropertyName};\r\n";
		if (config.GenerateDataAccessClasses)
			s += $"\t\tthis.{dataAccessPropertyName} = {dataAccessPropertyName};\r\n";
		s += $"\t}}\r\n";
		// INSERT
		s += $"\r\n";
		s += IdentText(GetSummary($"Insert new {ConvertNameToString(tableModelName)} record.")) + "\r\n";
		s += $"\t[HttpPost(\"[action]\")]\r\n";
		s += $"\tpublic async Task<{returnItemType}> Insert(\r\n";
		s += $"\t\t{tableModelName}{dtoSuffix} {paramName},\r\n";
		s += $"\t\tCancellationToken cancellationToken = default\r\n";
		s += $"\t) {{\r\n";
		if (config.GenerateDataAccessClasses) {
			s += $"\t\tvar db{prefixedParamName} = await {dataAccessPropertyName}.Insert({paramName}, cancellationToken);\r\n";
		}
		else {
			s += $"\t\tvar db{prefixedParamName} = new {tableModelName}();\r\n";
			s += $"\t\tCopyProperties({paramName}, db{prefixedParamName});\r\n";
			s += $"\t\t{modelPropertyName}.{tablePropertyName}.Add(db{prefixedParamName}); \r\n";
			s += $"\t\tawait {modelPropertyName}.SaveChangesAsync(cancellationToken);\r\n";
		}
		s += returnLine;
		s += $"\t}}\r\n";
		// SELECT
		s += $"\r\n";
		s += IdentText(GetSummary($"Select {ConvertNameToString(tableModelName)} record.")) + "\r\n";
		s += $"\t[HttpGet(\"[action]/{{{idName}:{primaryColumnKeyType}}}\")]\r\n";
		s += $"\tpublic async Task<{returnItemType}> Select(\r\n";
		s += $"\t\t{primaryColumnKeyType} {idName},\r\n";
		s += $"\t\tCancellationToken cancellationToken = default\r\n";
		s += $"\t) {{\r\n";
		s += $"\t\tvar db{prefixedParamName} = await {modelPropertyName}.{tablePropertyName}.FindAsync(cancellationToken, {idName});\r\n";
		s += $"\t\tif (db{prefixedParamName} == null)\r\n";
		s += (config.ControllerUseActionResult
			? $"\t\t\treturn NotFound($\"{ConvertNameToString(tableModelName)} ({{{idName}}}) record not found!\");"
			: $"\t\t\tthrow new Exception($\"{ConvertNameToString(tableModelName)} ({{{idName}}}) record not found!\");") + "\r\n";
		s += returnLine;
		s += $"\t}}\r\n";
		// UPDATE
		s += $"\r\n";
		s += IdentText(GetSummary($"Update {ConvertNameToString(tableModelName)} record.")) + "\r\n";
		s += $"\t[HttpPost(\"[action]\")]\r\n";
		s += $"\tpublic async Task<{returnItemType}> Update(\r\n";
		s += $"\t\t{tableModelName}{dtoSuffix} {paramName},\r\n";
		s += $"\t\tCancellationToken cancellationToken = default\r\n";
		s += $"\t) {{\r\n";
		s += $"\t\tvar db{prefixedParamName} = await {modelPropertyName}.{tablePropertyName}.FindAsync(cancellationToken, {paramName}.{primaryColumnKeyName});\r\n";
		s += $"\t\tif (db{prefixedParamName} == null)\r\n";
		s += (config.ControllerUseActionResult
			? $"\t\t\treturn NotFound($\"{ConvertNameToString(tableModelName)} ({{{paramName}.{primaryColumnKeyName}}}) record not found!\");"
			: $"\t\t\tthrow new Exception($\"{ConvertNameToString(tableModelName)} ({{{paramName}.{primaryColumnKeyName}}}) record not found!\");") + "\r\n";

		s += $"\t\tCopyProperties({paramName}, db{prefixedParamName});\r\n";
		s += $"\t\tawait {modelPropertyName}.SaveChangesAsync(cancellationToken);\r\n";
		s += returnLine;
		s += $"\t}}\r\n";
		// DELETE
		s += $"\r\n";
		s += IdentText(GetSummary($"Delete {ConvertNameToString(tableModelName)} record.")) + "\r\n";
		s += $"\t[HttpDelete(\"[action]/{{{idName}:{primaryColumnKeyType}}}\")]\r\n";
		s += $"\tpublic async Task<int> Delete(\r\n";
		s += $"\t\t{primaryColumnKeyType} {idName},\r\n";
		s += $"\t\tCancellationToken cancellationToken = default\r\n";
		s += $"\t) {{\r\n";
		s += $"\t\tvar db{prefixedParamName} = await {modelPropertyName}.{tablePropertyName}.FindAsync(cancellationToken, {idName});\r\n";
		s += $"\t\tif (db{prefixedParamName} == null)\r\n";
		s += $"\t\t\treturn 0;\r\n";
		s += $"\t\t{modelPropertyName}.{tablePropertyName}.Remove(db{prefixedParamName});\r\n";
		s += $"\t\treturn await {modelPropertyName}.SaveChangesAsync(cancellationToken);\r\n";
		s += $"\t}}";

		//if (!string.IsNullOrEmpty(fkItems)) {
		//	contents += "\r\n#region Foreign Key Methods\r\n";
		//	contents += IdentText(fkLists, 2);
		//	contents += "\r\n#endregion\r\n";
		//}
		contents += "\r\n";
		contents += IdentText(s) + "\r\n";

		var fkMethods = "";
		foreach (var row in columnRows) {
			var tableItemType = GetTableItemType(schemaName, tableName);
			var columnKeyType = GetColumnPropertyType(row);
			var columnKeyName = GetColumnPropertyName(row);
			var columnPropertyName = ToCamelCase(columnKeyName);
			var primarySchemaName = (string)row["PrimaryKeySchema"];
			var primaryTableName = (string)row["PrimaryKeyTable"];
			var primaryColumnName = (string)row["PrimaryKeyColumn"];

			// If column doesn not point to primary key then...
			if (string.IsNullOrEmpty(primarySchemaName) || string.IsNullOrEmpty(primarySchemaName) || string.IsNullOrEmpty(primarySchemaName))
				continue;
			// Return type.

			var fkReturnLine = config.GenerateDTO
				? $"var list = items.Select(x => CopyProperties(x, new {tableModelName}{dtoSuffix}(), true)).ToList();\r\nreturn list;"
				: $"return items;";

			if (config.ControllerUseActionResult)
				fkReturnLine = new Regex("return (?<obj>.*);").Replace(fkReturnLine, "return Ok(${obj});");

			var f = "";
			f += GetSummary($"Select by {ConvertNameToString(columnKeyName)}") + "\r\n";
			f += $"public async Task<{returnListType}> SelectBy{columnKeyName}(\r\n";
			f += $"\t\t{columnKeyType} {columnPropertyName},\r\n";
			f += $"\t\tCancellationToken cancellationToken = default\r\n";
			f += $"\t) {{\r\n";
			f += $"\tvar items = await {modelPropertyName}.{tablePropertyName}\r\n";
			f += $"\t\t.Where(x => x.{columnKeyName} == {columnPropertyName})\r\n";
			f += $"\t\t.ToListAsync(cancellationToken);\r\n";
			f += IdentText(fkReturnLine, 1) + "\r\n";
			f += "}\r\n";
			fkMethods += "\r\n" + f;
		}
		if (!string.IsNullOrEmpty(fkMethods)) {
			contents += "\r\n";
			fkMethods = "#region Select by Foreign Key\r\n" + fkMethods + "\r\n#endregion";
			contents += IdentText(fkMethods, 2);
		}

		contents += "\r\n";
		contents += IdentText(copyProperties, 2) + "\r\n";
		contents += "\r\n";
		contents += "\t}\r\n";
		// Add Data Transfer Object (DTO) code.
		contents += "\r\n";
		if (!string.IsNullOrEmpty(transferClass))
			contents += IdentText(transferClass, 1) + "\r\n";
		// -----NAMESPACE CONTENT END-----
		contents += "\r\n";
		contents += "}\r\n";
		// -----CONTENT END-----
		return true;
	}

	#endregion

	public static void GetChildren(
		ClassDiagramClass[] parent, ClassDiagramClass[] all,
		out ClassDiagramClass[] children, out ClassDiagramClass[] remaining
	) {
		parent = parent ?? Array.Empty<ClassDiagramClass>();
		if (parent.Length == 0) {
			// Return all root nodes.
			children = all.Where(x => x.ShowAsAssociation?.Any() != true).ToArray();
		}
		else {
			var parentTypes = parent.Select(x => x.Name.Split('.').Last()).ToArray();
			var c = new List<ClassDiagramClass>();
			foreach (var a in all) {
				if (a.ShowAsAssociation?.Any() == true) {
					if (a.ShowAsAssociation.Any(x => parentTypes.Contains(x.Type)))
						c.Add(a);
				}
			}
			children = c.ToArray();
		}
		remaining = all.Except(children).ToArray();
	}

	private static List<string> GetTableNames(string schemaName) {
		return FilterRows(schemaName)
			.Select(x => (string)x["TableName"])
			.Distinct()
			.OrderBy(x => x)
			.ToList();
	}

	private static List<DataRow> FilterRows(string schemaName = null, string tableName = null, string columnName = null) {
		var items = _columnInfoRows
			.Where(x => schemaName == null || (string)x["SchemaName"] == schemaName)
			.Where(x => tableName == null || (string)x["TableName"] == tableName)
			.Where(x => columnName == null || (string)x["Name"] == columnName)
			.OrderBy(x => (string)x["SchemaName"])
			.ThenBy(x => (string)x["TableName"])
			.ThenBy(x => (int)x["Index"])
			.ToList();
		var filtered = new List<DataRow>();
		foreach (var item in items) {
			var stcName = $"{item["SchemaName"]}.{item["TableName"]}.{item["Name"]}";
			// Filter table name.
			if (config.ExcludeFilter?.IsMatch(stcName) == true) {
				//Log("    Skip (Exclude Filter): {0}", stcName);
				continue;
			}
			if (config.IncludeFilter?.IsMatch(stcName) == false) {
				//Log("    Skip (Include Filter): {0}", stcName);
				continue;
			}
			//Log("    Add: {0}", stcName);
			filtered.Add(item);
		}
		return filtered;
	}

	/// <summary>Generate Clone and Copy Methods</summary>
	private static string GenerateCloneAndCopyMethods(string tableModelName, List<DataRow> columnRows, string dtoSuffix) {
		var s = "";
		s += "#region Clone and Copy Methods\r\n";
		s += "\r\n";
		s += "/// <summary>Clone to new object.</summary>\r\n";
		s += $"public {tableModelName} Clone(bool copyKey = false)\r\n";
		s += $"\t=> Copy(this, new {tableModelName}(), copyKey);\r\n";
		s += "\r\n";
		s += "/// <summary>Copy to existing object.</summary>\r\n";
		s += $"public {tableModelName} Copy({tableModelName} target, bool copyKey = false)\r\n";
		s += "\t=> Copy(this, target, copyKey);\r\n";
		s += "\r\n";
		s += "/// <summary>Copy to existing object.</summary>\r\n";
		s += $"public static {tableModelName} Copy({tableModelName}{dtoSuffix} source, {tableModelName} target, bool copyKey = false) {{\r\n";
		foreach (var row in columnRows) {
			var columnPropertyName = GetColumnPropertyName(row);
			if ((bool)row["IsPrimaryKey"])
				s += $"\tif (copyKey)\r\n\t";
			s += $"\ttarget.{columnPropertyName} = source.{columnPropertyName};\r\n";
		}
		s += "\treturn target;\r\n";
		s += "}\r\n";
		s += "\r\n";
		s += "#endregion";
		return s;
	}

	/// <summary>
	/// Generate Item which contains item from primary table. This code is added onto foreign table code.
	/// </summary>
	private static string GetOneToManyItems(
			string foreignSchemaName,
			string foreignTableName,
			string foreignColumnName,
			List<ClassDiagramClassProperty> showAsAssociation
		) {
		var rows = FilterRows(foreignSchemaName, foreignTableName, foreignColumnName)
			.Where(x => (string)x["PrimaryKeySchema"] != "")
			.Where(x => (string)x["PrimaryKeyTable"] != "")
			.Where(x => (string)x["PrimaryKeyColumn"] != "")
			.ToList();
		var s = "";
		for (int r = 0; r < rows.Count; r++) {
			var row = rows[r];
			var primaryIsNullable = (bool)row["IsNullable"];
			var primarySchemaName = (string)row["PrimaryKeySchema"];
			var primaryTableName = (string)row["PrimaryKeyTable"];
			var primaryColumnName = (string)row["PrimaryKeyColumn"];
			var primaryTableItemType = GetTableItemType(primarySchemaName, primaryTableName);
			var foreignTableItemType = GetTableItemType(foreignSchemaName, foreignTableName);
			var nullSign = primaryIsNullable ? "?" : "";
			// Item contains type of primary table type.
			//Log("       Add FK Item {0}.{1}.{2}", schemaName, tableName, columnName);
			s += "\r\n";
			s += GetSummary("Foreign key item") + "\r\n";
			s += "[ForeignKey(nameof(" + foreignColumnName + "))]\r\n";
			s += "public virtual " + primaryTableItemType + nullSign + " " + TrimIdAndSingularize(foreignColumnName, foreignTableItemType) + " { get; set; }";
			if (config.EnableNullableReferenceTypes && !primaryIsNullable)
				s += " = null!;";
			s += "\r\n";
			// Add item for class diagram.
			showAsAssociation.Add(new ClassDiagramClassProperty() {
				// Primary type.
				Type = $"{primaryTableItemType}",
				Name = TrimIdAndSingularize(foreignColumnName, foreignTableItemType),
			});
		}
		return s;
	}

	/// <summary>
	/// Generate List which contains items from foreign table. This code is added onto primary table code.
	/// </summary>
	private static string GetOneToManyLists(
			string primarySchemaName,
			string primaryTableName,
			string primaryColumnName
		) {
		// Get all foreign columns which points to this primary column.
		var rows = FilterRows()
			.Where(x => primarySchemaName == null || (string)x["PrimaryKeySchema"] == primarySchemaName)
			.Where(x => primaryTableName == null || (string)x["PrimaryKeyTable"] == primaryTableName)
			.Where(x => primaryColumnName == null || (string)x["PrimaryKeyColumn"] == primaryColumnName)
			.ToList();
		var s = "";
		for (int r = 0; r < rows.Count; r++) {
			var row = rows[r];
			// Get foreign and primary column information.
			var foreignColumnName = (string)row["Name"];
			var foreignTableName = (string)row["TableName"];
			var foreignSchemaName = (string)row["SchemaName"];
			var foreignTableItemType = GetTableItemType(foreignSchemaName, foreignTableName);
			var primaryTableItemType = GetTableItemType(primarySchemaName, primaryTableName);
			//Log("       Add FK Item {0}.{1}.{2}", schemaName, tableName, columnName);
			// Collection contains type of foreign table type.
			s += "\r\n";
			s += GetSummary($"Collection of {ConvertNameToString(pluralizer.Pluralize(foreignTableItemType))}. Foreign key relationship.") + "\r\n";
			var propertyName = TrimIdAndPluralize(foreignColumnName, foreignTableItemType, primaryTableItemType);
			// Make sure namespace won't clash with property name.
			var ftitNamespace = foreignTableItemType == propertyName
				? config.Namespace + "." + foreignTableItemType
				: foreignTableItemType;
			s += "[InverseProperty(nameof(" + ftitNamespace + "." + TrimIdAndSingularize(foreignColumnName, foreignTableItemType) + "))]\r\n";
			// "virtual" directive enables "transparent lazy loading".  
			// EF will at runtime create a new class (dynamic proxy) derived from your class and use it instead.
			// This new dynamically created class contains logic to load navigation property when accessed for the first time.
			// Non-virtual: var brandsAndProduct = db.Brands.Include("Products").Single(brand => brand.BrandID == 22);
			// Virtual:     var brandsAndProduct = pe.Brands.Where(brand => brand.BrandID == 22);
			s += $"public virtual ICollection<{foreignTableItemType}> {propertyName} {{ get; }}";
			if (config.EnableNullableReferenceTypes)
				s += $"\r\n\t= new List<{foreignTableItemType}>();";
			s += "\r\n";
		}
		return s;
	}

	private static bool useStringLengthAttribute(DataRow row) {
		var type = (string)row["Type"];
		var length = (int)row["Length"];
		return type.Contains("char") && length != -1;
	}

	private static bool useExternalCustomOption(DataRow row) {
		var customOptions = (int)row["CustomOptions"];
		var isExternal = (customOptions & 4) == 4;
		return isExternal;
	}

	private static string GetEntityUsings() {
		var s = "";
		if (config.EnableEntityFramework6 && config.EnableEntityFrameworkCore)
			s += "#if NETFRAMEWORK\r\n";
		if (config.EnableEntityFramework6) {
			// Support for Entity Framework 6 attributes (old).
			s += "using System.Data.Entity;\r\n";
			s += "using System.Data.Entity.Infrastructure;\r\n";
		}
		if (config.EnableEntityFramework6 && config.EnableEntityFrameworkCore)
			s += "#else\r\n";
		if (config.EnableEntityFrameworkCore) {
			// Support for Entity Framework Core attributes (new).
			s += "using Microsoft.EntityFrameworkCore;\r\n";
		}
		if (config.EnableEntityFramework6 && config.EnableEntityFrameworkCore)
			s += "#endif\r\n";
		return s;
	}

	private static string TrimIdAndSingularize(string foreignColumnName, string foreignTableItemType) {
		var newName = foreignColumnName;
		if (config.SingularizeForeignKeyItems) {
			if (newName.EndsWith("Id"))
				newName = newName.Substring(0, newName.Length - 2);
			if (pluralizer.IsPlural(newName))
				newName = pluralizer.Singularize(newName);
			// If property name not conflicting with existing property of declaring class name then...
			if (newName != foreignColumnName && newName != foreignTableItemType)
				return newName;
		}
		return newName + "_Item";
	}

	private static string TrimIdAndPluralize(string foreignColumnName, string foreignTableItemType, string primaryTableItemType) {
		if (!config.PluralizeForeignKeyLists)
			return $"{foreignTableItemType}_{foreignColumnName}_List";
		if (foreignColumnName.EndsWith("Id"))
			foreignColumnName = foreignColumnName.Substring(0, foreignColumnName.Length - 2);
		var pluralizedForeignColumnName = pluralizer.IsSingular(foreignColumnName)
			? pluralizer.Pluralize(foreignColumnName)
			: foreignColumnName;
		var pluralizedPrimaryTableItemType = pluralizer.IsSingular(primaryTableItemType)
				? pluralizer.Pluralize(primaryTableItemType)
				: primaryTableItemType;
		if (pluralizedForeignColumnName != pluralizedPrimaryTableItemType)
			return $"{foreignTableItemType}_{foreignColumnName}";
		var pluralizedForeignTableItemType = pluralizer.IsSingular(foreignTableItemType)
				? pluralizer.Pluralize(foreignTableItemType)
				: foreignTableItemType;
		return pluralizedForeignTableItemType;
	}


	#region ■ Text Helper

	public static string GetSummary(string s) {
		s = s.Trim('\r', '\n');
		// If cotnains line breaks then...
		s = s.Contains("\r") || s.Contains("\r")
			? "\r\n" + IdentText(s, 1, "/// ") + "\r\n/// "
			: s;
		s = $"/// <summary>{s}</summary>";
		return s;
	}

	/// <summary>
	/// Add or remove ident.
	/// </summary>
	/// <param name="s">String to ident.</param>
	/// <param name="tabs">Positive - add ident, negative - remove ident.</param>
	/// <param name="ident">Ident character</param>
	public static string IdentText(string s, int tabs = 1, string ident = "\t") {
		if (tabs == 0)
			return s;
		if (string.IsNullOrEmpty(s))
			return s;
		var sb = new StringBuilder();
		var tr = new StringReader(s);
		var prefix = string.Concat(Enumerable.Repeat(ident, tabs));
		string line;
		var lines = s.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
		for (int i = 0; i < lines.Length; i++) {
			line = lines[i];
			if (line != "") {
				// If add idents then...
				if (tabs > 0)
					sb.Append(prefix);
				// If remove idents then...
				else if (tabs < 0) {
					var count = 0;
					// Count how much idents could be removed
					while (line.Substring(count * ident.Length, ident.Length) == ident && count < tabs)
						count++;
					line = line.Substring(count * ident.Length);
				}
			}
			if (i < lines.Length - 1)
				sb.AppendLine(line);
			else
				sb.Append(line);
		}
		tr.Dispose();
		return sb.ToString();
	}

	public static string ToCamelCase(string s) {
		var words = s.Split(new[] { "_", " " }, StringSplitOptions.RemoveEmptyEntries);
		var leadWord = Regex.Replace(words[0], @"([A-Z])([A-Z]+|[a-z0-9]+)($|[A-Z]\w*)",
			x => x.Groups[1].Value.ToLower() + x.Groups[2].Value.ToLower() + x.Groups[3].Value);
		var tailWords = words
			.Skip(1)
			.Select(x => char.ToUpper(x[0]) + x.Substring(1))
			.ToArray();
		return $"{leadWord}{string.Join(string.Empty, tailWords)}";
	}

	#endregion

	#region ■ Convert C# Types 

	private static bool IsTypeAlias(string name) {
		return _typeAlias.ContainsValue(name);
	}

	/// <summary>Built-in types</summary>
	private static readonly Dictionary<Type, string> _typeAlias = new Dictionary<Type, string>
		{
		{ typeof(bool), "bool" },
		{ typeof(byte), "byte" },
		{ typeof(char), "char" },
		{ typeof(decimal), "decimal" },
		{ typeof(double), "double" },
		{ typeof(float), "float" },
		{ typeof(int), "int" },
		{ typeof(long), "long" },
		{ typeof(object), "object" },
		{ typeof(sbyte), "sbyte" },
		{ typeof(short), "short" },
		{ typeof(string), "string" },
		{ typeof(uint), "uint" },
		{ typeof(ulong), "ulong" },
		{ typeof(ushort), "ushort" },
		{ typeof(void), "void" }
	};

	/// <summary>
	/// Convert C# Type to string or alias. 
	/// </summary>
	public static string GetBuiltInTypeNameOrAlias(Type type) {
		if (type == null)
			throw new ArgumentNullException("type");
		var elementType = type.IsArray
				? type.GetElementType()
				: type;
		// Lookup alias for type
		string aliasName;
		if (_typeAlias.TryGetValue(elementType, out aliasName))
			return aliasName + (type.IsArray ? "[]" : "");
		// Note: All Nullable<T> are value types.
		if (type.IsValueType) {
			var underType = Nullable.GetUnderlyingType(type);
			if (underType != null)
				return GetBuiltInTypeNameOrAlias(underType) + "?";
		}
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
			var itemType = type.GetGenericArguments()[0];
			return string.Format("List<{0}>", GetBuiltInTypeNameOrAlias(itemType));
		}
		// Default to CLR type name
		return type.Name;
	}

	/// <summary>
	/// Get C# class name.
	/// </summary>
	public static string GetTableItemType(string schemaName, string tableName) {
		var name = tableName;
		// Check if table name override was found.
		var tableModelPV = GetEntityModelName(schemaName, tableName);
		if (!string.IsNullOrEmpty(tableModelPV))
			name = tableModelPV;
		if (config.SingularizeItemName && pluralizer.IsPlural(name))
			name = pluralizer.Singularize(name);
		return name;
	}

	/// <summary>
	/// Get C# table (DbSet) property name.
	/// </summary>
	public static string GetTablePropertyName(string name) {
		if (config.PluralizeTableName && pluralizer.IsSingular(name))
			name = pluralizer.Pluralize(name);
		return name;
	}

	/// <summary>
	/// Get C# type from column row.
	/// </summary>
	/// <param name="row">Column row</param>
	private static string GetColumnPropertyType(DataRow row) {
		var type = (string)row["Type"];
		var CustomType = (string)row["CustomType"];
		var IsNullable = (bool)row["IsNullable"];
		var sqlType = (SqlDataType)Enum.Parse(typeof(SqlDataType), type, true);
		var sysType = ToSystemType(sqlType);
		var csType = CustomType == ""
			? GetBuiltInTypeNameOrAlias(sysType)
			: CustomType;
		// If nullable then...
		if (IsNullable && sysType.IsValueType)
			csType += "?";
		return csType;
	}

	/// <summary>
	/// Get C# type from column row.
	/// </summary>
	/// <param name="row">Column row</param>
	private static string GetColumnPropertyName(DataRow row) {
		var name = (string)row["Name"];
		var isTypeAlias = IsTypeAlias(name);
		var namePrefix = isTypeAlias ? "@" : "";
		var csName = namePrefix + name;
		return csName;
	}

	#endregion

	#region ■ Convert SQL Data Types

	/// <summary>
	/// Convert SQL Data Type to C# type string.
	/// </summary>
	public static string ToSystemTypeCS(SqlDataType sqlType) {
		var t = ToSystemType(sqlType);
		var name = GetBuiltInTypeNameOrAlias(t);
		return name;
	}

	/// <summary>
	/// Convert SQL Data Type to C# type.
	/// </summary>
	public static Type ToSystemType(SqlDataType sqlType) {
		Type t = typeof(String);
		switch (sqlType) {
			case SqlDataType.BigInt:
				t = typeof(Int64);
				break;
			case SqlDataType.Bit:
				t = typeof(Boolean);
				break;
			case SqlDataType.Char:
			case SqlDataType.VarChar:
			case SqlDataType.VarCharMax:
				t = typeof(String);
				break;
			case SqlDataType.Date:
			case SqlDataType.DateTime:
			case SqlDataType.DateTime2:
			case SqlDataType.SmallDateTime:
				t = typeof(DateTime);
				break;
			case SqlDataType.DateTimeOffset:
				t = typeof(DateTimeOffset);
				break;
			case SqlDataType.Time:
				t = typeof(TimeSpan);
				break;
			case SqlDataType.Decimal:
			case SqlDataType.Money:
			case SqlDataType.SmallMoney:
				t = typeof(Decimal);
				break;
			case SqlDataType.Int:
				t = typeof(Int32);
				break;
			case SqlDataType.NChar:
			case SqlDataType.NText:
			case SqlDataType.NVarChar:
			case SqlDataType.NVarCharMax:
			case SqlDataType.Text:
				t = typeof(String);
				break;
			case SqlDataType.Real:
			case SqlDataType.Numeric:
			case SqlDataType.Float:
				t = typeof(Double);
				break;
			case SqlDataType.Timestamp:
			case SqlDataType.Binary:
				t = typeof(Byte[]);
				break;
			case SqlDataType.TinyInt:
				t = typeof(Byte);
				break;
			case SqlDataType.SmallInt:
				t = typeof(Int16);
				break;
			case SqlDataType.UniqueIdentifier:
				t = typeof(Guid);
				break;
			case SqlDataType.UserDefinedDataType:
			case SqlDataType.UserDefinedType:
			case SqlDataType.Variant:
			case SqlDataType.Image:
				t = typeof(Object);
				break;
			default:
				t = typeof(String);
				break;
		}
		return t;
	}

	#endregion

	#region ■ SQL Helper

	public static string GetEntityModelName(string schema = null, string table = null, string column = null) {
		return GetProperty("EntityModelName", schema, table, column);
	}

	public static string GetEntityInheritance(string schema = null, string table = null, string column = null) {
		return GetProperty("EntityInheritance", schema, table, column);
	}

	public static string GetDescription(string schema = null, string table = null, string column = null) {
		return GetProperty("MS_Description", schema, table, column);
	}

	public static int SetDescription(string value, string schema = null, string table = null, string column = null) {
		return SetProperty("MS_Description", value, schema, table, column);
	}

	public static string GetProperty(string name, string schema = null, string table = null, string column = null) {
		var level0 = string.IsNullOrEmpty(schema) ? null : "SCHEMA";
		var level1 = string.IsNullOrEmpty(table) ? null : "TABLE";
		var level2 = string.IsNullOrEmpty(column) ? null : "COLUMN";
		var row = fn_listextendedproperty(
				name,
				level0, schema,
				level1, table,
				level2, column);
		var value = row == null ? null : row["value"] as string;
		return value;
	}

	public static int SetProperty(string name, string value, string schema = null, string table = null, string column = null) {
		var level0 = string.IsNullOrEmpty(schema) ? null : "SCHEMA";
		var level1 = string.IsNullOrEmpty(table) ? null : "TABLE";
		var level2 = string.IsNullOrEmpty(column) ? null : "COLUMN";
		var row = fn_listextendedproperty(
			name,
			level0, schema,
			level1, table,
			level2, column);
		if (value == null) {
			if (row == null)
				return 0;
			return sp_extendedproperty(
				-1,
				name, null,
				level0, schema,
				level1, table,
				level2, column
			);
		}
		return sp_extendedproperty(
				row == null ? 1 : 0,
				name, value,
				level0, schema,
				level1, table,
				level2, column
			);
	}

	public static DataRow fn_listextendedproperty(
			string name,
			string level0type = null,
			string level0name = null,
			string level1type = null,
			string level1name = null,
			string level2type = null,
			string level2name = null
	) {
		var cmd = new SqlCommand("SELECT * FROM sys.fn_listextendedproperty(@property_name, @level0_object_type, @level0_object_name, @level1_object_type, @level1_object_name, @level2_object_type, @level2_object_name)");
		cmd.CommandType = CommandType.Text;
		var p = cmd.Parameters;
		p.AddWithValue("@property_name", name);
		p.Add("@level0_object_type", SqlDbType.VarChar, 128).Value = level0type ?? (object)DBNull.Value;
		p.Add("@level0_object_name", SqlDbType.VarChar, 128).Value = level0name ?? (object)DBNull.Value;
		p.Add("@level1_object_type", SqlDbType.VarChar, 128).Value = level1type ?? (object)DBNull.Value;
		p.Add("@level1_object_name", SqlDbType.VarChar, 128).Value = level1name ?? (object)DBNull.Value;
		p.Add("@level2_object_type", SqlDbType.VarChar, 128).Value = level2type ?? (object)DBNull.Value;
		p.Add("@level2_object_name", SqlDbType.VarChar, 128).Value = level2name ?? (object)DBNull.Value;
		var data = ExecuteDataRow(config.ModelConnection, cmd);
		cmd.Dispose();
		return data;
	}

	private static int sp_extendedproperty(
	int action,
	string name,
	object value = null,
	string level0type = null,
	string level0name = null,
	string level1type = null,
	string level1name = null,
	string level2type = null,
	string level2name = null
) {
		// -1 - Delete, 0 - Update, 1 - Add.
		var sp = "sys.sp_updateextendedproperty";
		if (action == -1)
			sp = "sys.sp_dropextendedproperty";
		if (action == 1)
			sp = "sys.sp_addextendedproperty";
		var cmd = new SqlCommand(sp);
		cmd.CommandType = CommandType.StoredProcedure;
		var p = cmd.Parameters;
		p.AddWithValue("@name", name);
		if (value != null)
			p.AddWithValue("@value", value);
		p.Add("@level0type", SqlDbType.VarChar, 128).Value = level0type ?? (object)DBNull.Value;
		p.Add("@level0name", SqlDbType.VarChar, 128).Value = level0name ?? (object)DBNull.Value;
		p.Add("@level1type", SqlDbType.VarChar, 128).Value = level1type ?? (object)DBNull.Value;
		p.Add("@level1name", SqlDbType.VarChar, 128).Value = level1name ?? (object)DBNull.Value;
		p.Add("@level2type", SqlDbType.VarChar, 128).Value = level2type ?? (object)DBNull.Value;
		p.Add("@level2name", SqlDbType.VarChar, 128).Value = level2name ?? (object)DBNull.Value;
		var data = ExecuteNonQuery(config.ModelConnection, cmd);
		cmd.Dispose();
		return data;
	}

	public static int ExecuteNonQuery(string connectionString, SqlCommand cmd, string comment = null, int? timeout = null) {
		//var sql = ToSqlCommandString(cmd);
		var cb = new SqlConnectionStringBuilder(connectionString);
		if (timeout.HasValue) {
			cmd.CommandTimeout = timeout.Value;
			cb.ConnectTimeout = timeout.Value;
		}
		var conn = new SqlConnection(cb.ConnectionString);
		cmd.Connection = conn;
		conn.Open();
		int rv = cmd.ExecuteNonQuery();
		cmd.Dispose();
		// Dispose calls conn.Close() internally.
		conn.Dispose();
		return rv;
	}

	public static T ExecuteDataSet<T>(string connectionString, SqlCommand cmd, string comment = null) where T : DataSet {
		var conn = new SqlConnection(connectionString);
		cmd.Connection = conn;
		conn.Open();
		var adapter = new SqlDataAdapter(cmd);
		var ds = Activator.CreateInstance<T>();
		int rowsAffected = ds.GetType() == typeof(DataSet)
				? adapter.Fill(ds)
				: adapter.Fill(ds, ds.Tables[0].TableName);
		adapter.Dispose();
		cmd.Dispose();
		// Dispose calls conn.Close() internally.
		conn.Dispose();
		return ds;
	}

	public static DataSet ExecuteDataSet(string connectionString, SqlCommand cmd, string comment = null) {
		return ExecuteDataSet<DataSet>(connectionString, cmd, comment);
	}

	public static DataTable ExecuteDataTable(SqlCommand cmd, string comment = null) {
		var ds = ExecuteDataSet(config.ModelConnection, cmd, comment);
		if (ds != null && ds.Tables.Count > 0)
			return ds.Tables[0];
		return null;
	}

	public static DataTable ExecuteDataTable(string connectionString, SqlCommand cmd, string comment = null) {
		var ds = ExecuteDataSet(connectionString, cmd, comment);
		if (ds != null && ds.Tables.Count > 0)
			return ds.Tables[0];
		return null;
	}

	public static DataRow ExecuteDataRow(string connectionString, SqlCommand cmd, string comment = null) {
		var table = ExecuteDataTable(connectionString, cmd, comment);
		if (table != null && table.Rows.Count > 0)
			return table.Rows[0];
		return null;
	}

	#endregion

	#region ■ File Writing

	public static bool IsDifferent(string name, byte[] bytes) {
		if (bytes == null)
			throw new ArgumentNullException(nameof(bytes));
		var fi = new FileInfo(name);
		// If file doesn't exists or file size is different then...
		if (!fi.Exists || fi.Length != bytes.Length)
			return true;
		var algorithm = System.Security.Cryptography.SHA256.Create();
		var byteHash = algorithm.ComputeHash(bytes);
		var fileBytes = File.ReadAllBytes(fi.FullName);
		var fileHash = algorithm.ComputeHash(fileBytes);
		for (int i = 0; i < byteHash.Length; i++) {
			if (byteHash[i] != fileHash[i])
				return true;
		}

		return false;
	}

	public static bool WriteIfDifferent(string name, string contents) {
		var bytes = System.Text.Encoding.UTF8.GetBytes(contents);
		var isDifferent = IsDifferent(name, bytes);
		if (isDifferent)
			File.WriteAllText(name, contents);
		return isDifferent;
	}

	public static bool WriteIfDifferent(string name, byte[] bytes) {
		var isDifferent = IsDifferent(name, bytes);
		if (isDifferent)
			File.WriteAllBytes(name, bytes);
		return isDifferent;
	}

	/// <summary>
	/// Save file content if different.
	/// </summary>
	private void SaveFile(string path, string contents) {
		var fullPath = Path.Combine(TemplateFile.Directory.FullName, path);
		// var getContents = GenerationEnvironment.ToString()
		WriteIfDifferent(fullPath, contents);
		//GenerationEnvironment.Clear();
	}

	/// <summary>
	/// Remove all *.cs files.
	/// </summary>
	/// <param name="name"></param>
	private void CleanupFolder(string name) {
		var dir = new DirectoryInfo(GetPath(name));
		if (!dir.Exists)
			dir.Create();
		// Cleanup folder.
		var files = dir.GetFiles("*.cs");
		for (var f = 0; f < files.Length; f++)
			files[f].Delete();
	}

	private static string SeriallizeToXmlString(object o, Encoding encoding = null, bool omitXmlDeclaration = false, string comment = null, bool indent = true) {
		if (o == null)
			return null;
		// Create serialization settings.
		encoding = encoding ?? Encoding.UTF8;
		var settings = new XmlWriterSettings();
		settings.OmitXmlDeclaration = omitXmlDeclaration;
		settings.Encoding = encoding;
		settings.Indent = indent;
		// Serialize.
		var extraTypes = new Type[] { typeof(string) };
		var serializer = new XmlSerializer(o.GetType(), extraTypes);
		// Serialize in memory first, so file will be locked for shorter times.
		var ms = new MemoryStream();
		var xw = XmlWriter.Create(ms, settings);
		try {
			lock (serializer) {
				if (!string.IsNullOrEmpty(comment)) {
					xw.WriteStartDocument();
					xw.WriteComment(comment);
				}
				if (omitXmlDeclaration) {
					//Create our own namespaces for the output
					var ns = new XmlSerializerNamespaces();
					//Add an empty namespace and empty value
					ns.Add("", "");
					serializer.Serialize(xw, o, ns);
				}
				else {
					serializer.Serialize(xw, o);
				}
				if (!string.IsNullOrEmpty(comment)) {
					xw.WriteEndDocument();
				}
				// Make sure that all data flushed into memory stream.
				xw.Flush();
			}
		}
		catch (Exception) {
			throw;
		}
		finally {
			// This will close underlying MemoryStream too.
			xw.Close();
		}
		// ToArray will return all bytes from memory stream despite it being closed.
		// Bytes will start with Byte Order Mark(BOM) and are ready to write into file.
		var xmlBytes = ms.ToArray();
		// Use StreamReader to remove Byte Order Mark(BOM).
		var ms2 = new MemoryStream(xmlBytes);
		var sr = new StreamReader(ms2, true);
		var xmlString = sr.ReadToEnd();
		// This will close underlying MemoryStream too.
		sr.Close();
		return xmlString;
	}

	#endregion

}

#region ■ Configuration

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class RequiredAttribute : Attribute { }

public class Config {

	public bool Enabled = true;
	// Move required to top to read it first.
	[Required] public string Namespace;
	[Required] public string ModelClassName;
	[Required] public string ModelConnection;
	// Item
	public bool GenerateCloneAndCopyMethods;
	public bool GenerateOneToManyItems;
	public bool GenerateOneToManyLists;
	// Context
	public bool GenerateContext = true;
	public string ContextNamespace;
	public string ContextDefaultSchema = "dbo";
	// Controllers
	public bool GenerateControllers = true;
	public string ControllerNamespace;
	public bool ControllerUseActionResult;
	public string ControllerBaseClassList;
	public bool GenerateDataAccessClasses;
	// Generate DTO
	public bool GenerateDTO;
	// Other
	public string DefaultColumnSummary = "{0}";
	public string DefaultTableSummary = "{0}";
	public string OutputPath;
	public bool UseDateTimeKindUtcAttribute;
	public bool UseDateTimeKindUtcProperty;
	public bool StoreEnumDataAsStrings;
	public DataTable OverrideColumnInfo;
	public bool EnableInernalFiles;
	public Regex ExcludeFilter = new Regex("\b__");
	public Regex IncludeFilter = new Regex("");
	public bool AddGeneratedCodeAttribute = true;
	// Pluralize/Singularize
	public bool PluralizeTableName;
	public bool SingularizeItemName;
	public bool PluralizeForeignKeyLists;
	public bool SingularizeForeignKeyItems;
	// C# Features.
	public bool EnableNullableReferenceTypes = true;
	public bool EnableEntityFramework6 = true;
	public bool EnableEntityFrameworkCore = true;

	private ConfigFile _Config;

	public void LoadConfig(FileInfo configFile) {
		//Log(new string('-', 64));
		//Log($"CONFIG: {configFile.Name}");
		//Log(new string('-', 64));
		// Load configuration.
		_Config = new ConfigFile(configFile.FullName);
		var props = GetType().GetFields();
		foreach (var prop in props) {
			var value =
				_Config.AppSettings[prop.Name]?.Value ??
				_Config.ConnectionStrings[prop.Name]?.ConnectionString;
			if (value == null) {
				// If attribute is not required then continue...
				if (!Attribute.IsDefined(prop, typeof(RequiredAttribute)))
					continue;
				var errorMessage = $"Config setting '{prop.Name}' not found inside '{configFile.Name}' file!";
				MainModel.Log($"Error: {errorMessage}");
				//continue;
				throw new Exception(errorMessage);
			}
			object result;
			switch (prop.Name) {
				case nameof(ExcludeFilter):
				case nameof(IncludeFilter):
					result = GetRegex(value);
					break;
				case nameof(OverrideColumnInfo):
					// Enum options.
					result = GetTable(value);
					break;
				default:
					TryParse(value, prop.FieldType, out result);
					break;
			}
			prop.SetValue(this, result);
			if (prop.Name == nameof(Namespace)) {
				ControllerNamespace = Namespace;
				ContextNamespace = Namespace;
			}
		}
	}

	private bool TryParse(string value, Type t, out object result) {
		if (t == typeof(string)) {
			result = value;
			return true;
		}
		if (t.IsEnum) {
			var retValue = value == null ? false : Enum.IsDefined(t, value);
			result = retValue ? Enum.Parse(t, value) : default;
			return retValue;
		}
		var tryParseMethod = t.GetMethod("TryParse",
			System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public, null,
			new[] { typeof(string), t.MakeByRefType() }, null);
		var parameters = new object[] { value, null };
		var retVal = (bool)tryParseMethod.Invoke(null, parameters);
		result = parameters[1];
		return retVal;
	}

	private Regex GetRegex(string value) {
		if (string.IsNullOrEmpty(value))
			return null;
		value = string.Join("|", value
			.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(x => x.Trim())
			.Where(x => !string.IsNullOrEmpty(x)));
		// Log($"{settingName}: {value}");
		return new Regex(value, RegexOptions.IgnoreCase);
	}

	private DataTable GetTable(string csvValue) {
		var table = new DataTable();
		if (string.IsNullOrEmpty(csvValue))
			return table;
		var lines = csvValue
		.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
		.Select(x => x.Trim())
		.Where(x => !string.IsNullOrEmpty(x)).ToArray();
		var columns = lines[0].Split(',');
		foreach (var column in columns)
			table.Columns.Add(column);
		for (int i = 1; i < lines.Length; i++)
			table.Rows.Add(lines[i].Split(','));
		return table;
	}
}

/// <summary>
/// Parse *.config XML file.
/// </summary>
public class ConfigFile {
	/// <summary>
	/// Configuration file.
	/// </summary>
	public System.Configuration.Configuration Configuration { get; }
	/// <summary>
	/// Collection of configuration/appSettings/add elements.
	/// </summary>
	public System.Configuration.KeyValueConfigurationCollection AppSettings { get; }
	/// <summary>
	/// Collection of configuration/connectionStrings/add elements.
	/// </summary>
	public System.Configuration.ConnectionStringSettingsCollection ConnectionStrings { get; }
	/// <summary>
	/// Load *.config XML file.
	/// </summary>
	/// <param name="configFileName">Path to *.config XML file.</param>
	public ConfigFile(string configFileName) {
		var fileMap = new System.Configuration.ExeConfigurationFileMap();
		fileMap.ExeConfigFilename = configFileName;
		Configuration = System.Configuration.ConfigurationManager
			.OpenMappedExeConfiguration(fileMap, System.Configuration.ConfigurationUserLevel.None);
		AppSettings = ((System.Configuration.AppSettingsSection)Configuration
			.GetSection(Configuration.AppSettings.SectionInformation.Name))
			.Settings;
		ConnectionStrings = ((System.Configuration.ConnectionStringsSection)Configuration
			.GetSection(Configuration.ConnectionStrings.SectionInformation.Name))
			.ConnectionStrings;
	}
}

#endregion

#region ■ SQL Types

[Flags]
public enum GenType {
	Enum = 1,
	Class = 2,
	Interface = 3,
}

public enum GenItemType {
	InternalClass = 1,
	ExternalClass = 2,
	TransferClass = 3,
}


public enum SqlDataType {
	BigInt = 1,
	Binary = 2,
	Bit = 3,
	Char = 4,
	Date = 36,
	DateTime = 6,
	DateTime2 = 39,
	DateTimeOffset = 38,
	Decimal = 7,
	Float = 8,
	Geography = 43,
	Geometry = 42,
	HierarchyId = 41,
	Image = 9,
	Int = 10,
	Money = 11,
	NChar = 12,
	None = 0,
	NText = 13,
	Numeric = 35,
	NVarChar = 14,
	NVarCharMax = 15,
	Real = 16,
	SmallDateTime = 17,
	SmallInt = 18,
	SmallMoney = 19,
	SysName = 34,
	Text = 20,
	Time = 37,
	Timestamp = 21,
	TinyInt = 22,
	UniqueIdentifier = 23,
	UserDefinedDataType = 24,
	UserDefinedTableType = 40,
	UserDefinedType = 25,
	VarBinary = 28,
	VarBinaryMax = 29,
	VarChar = 30,
	VarCharMax = 31,
	Variant = 32,
	Xml = 33
}

#endregion

#region ■ Class Diagram File Structure

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false)]
public partial class ClassDiagram {
	[XmlElement("Class")]
	public List<ClassDiagramClass> Class { get; set; }
	[XmlAttribute] public byte MajorVersion { get; set; }
	[XmlAttribute] public byte MinorVersion { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public partial class ClassDiagramClass {
	public ClassDiagramClassPosition Position { get; set; }
	public ClassDiagramClassTypeIdentifier TypeIdentifier { get; set; }
	[XmlArrayItem("Property", IsNullable = false)] public List<ClassDiagramClassProperty> ShowAsAssociation { get; set; }
	[XmlAttribute] public string Name { get; set; }
	[XmlAttribute] public bool Collapsed { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public partial class ClassDiagramClassPosition {
	[XmlAttribute] public decimal X { get; set; }
	[XmlAttribute] public decimal Y { get; set; }
	[XmlAttribute] public decimal Width { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public partial class ClassDiagramClassTypeIdentifier {
	public string HashCode { get; set; }
	public string FileName { get; set; }
}

[Serializable]
[XmlType(AnonymousType = true)]
public partial class ClassDiagramClassProperty {
	/// <summary>Used for MainModel script only.</summary>
	[XmlIgnore] public string Type { get; set; }
	[XmlAttribute] public string Name { get; set; }
}

#endregion

#region ■ Pluralizer.NET

/*
<Title>Pluralize.NET</Title>
<Authors>Sarath KCM</Authors>
<Description>Pluralize or singularize any word.C# Port of Blake Embrey's pluralize library for Javascript.</Description>
<PackageLicenseUrl>https://github.com/sarathkcm/Pluralize.NET/blob/master/LICENCE</PackageLicenseUrl>
<PackageProjectUrl>https://github.com/sarathkcm/Pluralize.NET</PackageProjectUrl>
*/

public class PluralizerRule {
	public Regex Condition { get; set; }
	public string ReplaceWith { get; set; }
}

public class Pluralizer {

	protected readonly IList<PluralizerRule> _pluralRules = GetPluralRules();
	protected readonly IList<PluralizerRule> _singularRules = GetSingularRules();
	protected readonly ICollection<string> _uncountables = GetUncountables();
	protected readonly IDictionary<string, string> _irregularPlurals = GetIrregularPlurals();
	protected readonly IDictionary<string, string> _irregularSingles = GetIrregularSingulars();

	private static readonly Regex _replacementRegex = new Regex("\\$(\\d{1,2})");

	public string Pluralize(string word) {
		return Transform(word, _irregularSingles, _irregularPlurals, _pluralRules);
	}

	public string Singularize(string word) {
		return Transform(word, _irregularPlurals, _irregularSingles, _singularRules);
	}

	public bool IsSingular(string word) {
		return Singularize(word) == word;
	}

	public bool IsPlural(string word) {
		return Pluralize(word) == word;
	}

	public string Format(string word, int count, bool inclusive = false) {
		var pluralized = count == 1 ?
			Singularize(word) : Pluralize(word);
		return (inclusive ? count + " " : "") + pluralized;
	}

	public void AddPluralRule(Regex rule, string replacement) {
		_pluralRules.Add(new PluralizerRule {
			Condition = rule,
			ReplaceWith = replacement
		});
	}

	public void AddPluralRule(string rule, string replacement) {
		var regexRule = SanitizeRule(rule);
		_pluralRules.Add(new PluralizerRule {
			Condition = regexRule,
			ReplaceWith = replacement
		});
	}

	public void AddSingularRule(Regex rule, string replacement) {
		_singularRules.Add(new PluralizerRule {
			Condition = rule,
			ReplaceWith = replacement
		});
	}

	public void AddSingularRule(string rule, string replacement) {
		var regexRule = SanitizeRule(rule);
		_singularRules.Add(new PluralizerRule {
			Condition = regexRule,
			ReplaceWith = replacement
		});
	}

	public void AddUncountableRule(string word) {
		_uncountables.Add(word);
	}

	public void AddUncountableRule(Regex rule) {
		_pluralRules.Add(new PluralizerRule {
			Condition = rule,
			ReplaceWith = "$0"
		});
		_singularRules.Add(new PluralizerRule {
			Condition = rule,
			ReplaceWith = "$0"
		});
	}

	public void AddIrregularRule(string single, string plural) {
		_irregularSingles.Add(single, plural);
		_irregularPlurals.Add(plural, single);
	}

	private Regex SanitizeRule(string rule) {
		return new Regex($"^{rule}$", RegexOptions.IgnoreCase);
	}

	private string RestoreCase(string originalWord, string newWord) {
		// Tokens are an exact match.
		if (originalWord == newWord)
			return newWord;
		// Lower cased words. E.g. "hello".
		if (originalWord == originalWord.ToLower())
			return newWord.ToLower();
		// Upper cased words. E.g. "HELLO".
		if (originalWord == originalWord.ToUpper())
			return newWord.ToUpper();
		// Title cased words. E.g. "Title".
		if (originalWord[0] == char.ToUpper(originalWord[0]))
			return char.ToUpper(newWord[0]) + newWord.Substring(1);
		// Lower cased words. E.g. "test".
		return newWord.ToLower();
	}

	private string ApplyRules(string token, string originalWord, IList<PluralizerRule> rules) {
		// Empty string or doesn't need fixing.
		if (string.IsNullOrEmpty(token) || _uncountables.Contains(GetLastCapitalizedWord(token)))
			return originalWord;
		// Iterate over the sanitization rules and use the first one to match.
		// Iterate backwards since specific/custom rules can be appended
		for (var i = rules.Count - 1; i >= 0; i--) {
			var rule = rules[i];
			// If the rule passes, return the replacement.
			if (rule.Condition.IsMatch(originalWord)) {
				var match = rule.Condition.Match(originalWord);
				var matchString = match.Groups[0].Value;
				if (string.IsNullOrWhiteSpace(matchString))
					return rule.Condition.Replace(originalWord, GetReplaceMethod(originalWord[match.Index - 1].ToString(), rule.ReplaceWith), 1);
				return rule.Condition.Replace(originalWord, GetReplaceMethod(matchString, rule.ReplaceWith), 1);
			}
		}
		return originalWord;
	}

	private MatchEvaluator GetReplaceMethod(string originalWord, string replacement) {
		return match => {
			return RestoreCase(originalWord, _replacementRegex.Replace(replacement, m => match.Groups[int.Parse(m.Groups[1].Value)].Value));
		};
	}

	private string Transform(string word, IDictionary<string, string> replacables,
		IDictionary<string, string> keepables, IList<PluralizerRule> rules) {
		if (keepables.ContainsKey(word))
			return word;
		if (replacables.TryGetValue(word, out string token))
			return RestoreCase(word, token);
		return ApplyRules(word, word, rules);
	}

	public static IList<PluralizerRule> GetSingularRules() {
		return new List<PluralizerRule> {
			// rules are ordered more generic first
			new PluralizerRule { Condition = new Regex("s$", RegexOptions.IgnoreCase), ReplaceWith = ""},
			new PluralizerRule { Condition = new Regex("(ss)$", RegexOptions.IgnoreCase), ReplaceWith = "$1"},
			new PluralizerRule { Condition = new Regex("(wi|kni|(?:after|half|high|low|mid|non|night|[^\\w]|^)li)ves$", RegexOptions.IgnoreCase), ReplaceWith = "$1fe"},
			new PluralizerRule { Condition = new Regex("(ar|(?:wo|[ae])l|[eo][ao])ves$", RegexOptions.IgnoreCase), ReplaceWith = "$1f"},
			new PluralizerRule { Condition = new Regex("ies$", RegexOptions.IgnoreCase), ReplaceWith ="y"},
			new PluralizerRule { Condition = new Regex("\\b([pl]|zomb|(?:neck|cross)?t|coll|faer|food|gen|goon|group|lass|talk|goal|cut)ies$", RegexOptions.IgnoreCase), ReplaceWith = "$1ie" },
			new PluralizerRule { Condition = new Regex("\\b(mon|smil)ies$", RegexOptions.IgnoreCase), ReplaceWith = "$1ey"},
			new PluralizerRule { Condition = new Regex("\\b((?:tit)?m|l)ice$", RegexOptions.IgnoreCase), ReplaceWith = "$1ouse"},
			new PluralizerRule { Condition = new Regex("(seraph|cherub)im$", RegexOptions.IgnoreCase), ReplaceWith = "$1"},
			new PluralizerRule { Condition = new Regex("(x|ch|ss|sh|zz|tto|go|cho|alias|[^aou]us|t[lm]as|gas|(?:her|at|gr)o|[aeiou]ris)(?:es)?$", RegexOptions.IgnoreCase), ReplaceWith = "$1"},
			new PluralizerRule { Condition = new Regex("(analy|diagno|parenthe|progno|synop|the|empha|cri|ne)(?:sis|ses)$", RegexOptions.IgnoreCase), ReplaceWith = "$1sis"},
			new PluralizerRule { Condition = new Regex("(movie|twelve|abuse|e[mn]u)s$", RegexOptions.IgnoreCase), ReplaceWith = "$1"},
			new PluralizerRule { Condition = new Regex("(test)(?:is|es)$", RegexOptions.IgnoreCase), ReplaceWith = "$1is"},
			new PluralizerRule { Condition = new Regex("(alumn|syllab|octop|vir|radi|nucle|fung|cact|stimul|termin|bacill|foc|uter|loc|strat)(?:us|i)$", RegexOptions.IgnoreCase), ReplaceWith = "$1us"},
			new PluralizerRule { Condition = new Regex("(agend|addend|millenni|dat|extrem|bacteri|desiderat|strat|candelabr|errat|ov|symposi|curricul|quor)a$", RegexOptions.IgnoreCase), ReplaceWith = "$1um"},
			new PluralizerRule { Condition = new Regex("(apheli|hyperbat|periheli|asyndet|noumen|phenomen|criteri|organ|prolegomen|hedr|automat)a$", RegexOptions.IgnoreCase), ReplaceWith = "$1on"},
			new PluralizerRule { Condition = new Regex("(alumn|alg|vertebr)ae$", RegexOptions.IgnoreCase), ReplaceWith = "$1a"},
			new PluralizerRule { Condition = new Regex("(cod|mur|sil|vert|ind)ices$", RegexOptions.IgnoreCase), ReplaceWith = "$1ex"},
			new PluralizerRule { Condition = new Regex("(matr|append)ices$", RegexOptions.IgnoreCase), ReplaceWith = "$1ix"},
			new PluralizerRule { Condition = new Regex("(pe)(rson|ople)$", RegexOptions.IgnoreCase), ReplaceWith = "$1rson"},
			new PluralizerRule { Condition = new Regex("(child)ren$", RegexOptions.IgnoreCase), ReplaceWith = "$1"},
			new PluralizerRule { Condition = new Regex("(eau)x?$", RegexOptions.IgnoreCase), ReplaceWith = "$1"},
			new PluralizerRule { Condition = new Regex("men$", RegexOptions.IgnoreCase), ReplaceWith = "man" },
			new PluralizerRule { Condition = new Regex("[^aeiou]ese$", RegexOptions.IgnoreCase), ReplaceWith = "$0"},
			new PluralizerRule { Condition = new Regex("deer$", RegexOptions.IgnoreCase), ReplaceWith = "$0"},
			new PluralizerRule { Condition = new Regex("fish$", RegexOptions.IgnoreCase), ReplaceWith = "$0"},
			new PluralizerRule { Condition = new Regex("measles$", RegexOptions.IgnoreCase), ReplaceWith = "$0"},
			new PluralizerRule { Condition = new Regex("o[iu]s$", RegexOptions.IgnoreCase), ReplaceWith = "$0"},
			new PluralizerRule { Condition = new Regex("pox$", RegexOptions.IgnoreCase), ReplaceWith = "$0"},
			new PluralizerRule { Condition = new Regex("sheep$", RegexOptions.IgnoreCase), ReplaceWith = "$0" }
		};
	}

	public static IList<PluralizerRule> GetPluralRules() {
		return new List<PluralizerRule> {
            // rules are ordered more generic first
            new PluralizerRule { Condition = new Regex("s?$",RegexOptions.IgnoreCase), ReplaceWith = "s" },
			new PluralizerRule { Condition = new Regex("[^\u0000-\u007F]$",RegexOptions.IgnoreCase),  ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("([^aeiou]ese)$", RegexOptions.IgnoreCase), ReplaceWith = "$1" },
			new PluralizerRule { Condition = new Regex("(ax|test)is$",RegexOptions.IgnoreCase),  ReplaceWith = "$1es" },
			new PluralizerRule { Condition = new Regex("(alias|[^aou]us|t[lm]as|gas|ris)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1es" },
			new PluralizerRule { Condition = new Regex("(e[mn]u)s?$",RegexOptions.IgnoreCase),  ReplaceWith = "$1s" },
			new PluralizerRule { Condition = new Regex("([^l]ias|[aeiou]las|[ejzr]as|[iu]am)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1" },
			new PluralizerRule { Condition = new Regex("(alumn|syllab|vir|radi|nucle|fung|cact|stimul|termin|bacill|foc|uter|loc|strat)(?:us|i)$", RegexOptions.IgnoreCase), ReplaceWith = "$1i" },
			new PluralizerRule { Condition = new Regex("(alumn|alg|vertebr)(?:a|ae)$",RegexOptions.IgnoreCase), ReplaceWith = "$1ae" },
			new PluralizerRule { Condition = new Regex("(seraph|cherub)(?:im)?$",RegexOptions.IgnoreCase), ReplaceWith = "$1im" },
			new PluralizerRule { Condition = new Regex("(her|at|gr)o$", RegexOptions.IgnoreCase), ReplaceWith = "$1oes" },
			new PluralizerRule { Condition = new Regex("(agend|addend|millenni|dat|extrem|bacteri|desiderat|strat|candelabr|errat|ov|symposi|curricul|automat|quor)(?:a|um)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1a" },
			new PluralizerRule { Condition = new Regex("(apheli|hyperbat|periheli|asyndet|noumen|phenomen|criteri|organ|prolegomen|hedr|automat)(?:a|on)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1a" },
			new PluralizerRule { Condition = new Regex("sis$",RegexOptions.IgnoreCase), ReplaceWith = "ses" },
			new PluralizerRule { Condition = new Regex("(?:(kni|wi|li)fe|(ar|l|ea|eo|oa|hoo)f)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1$2ves" },
			new PluralizerRule { Condition = new Regex("([^aeiouy]|qu)y$",RegexOptions.IgnoreCase),  ReplaceWith = "$1ies" },
			new PluralizerRule { Condition = new Regex("([^ch][ieo][ln])ey$",RegexOptions.IgnoreCase),  ReplaceWith = "$1ies" },
			new PluralizerRule { Condition = new Regex("(x|ch|ss|sh|zz)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1es" },
			new PluralizerRule { Condition = new Regex("(matr|cod|mur|sil|vert|ind|append)(?:ix|ex)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1ices" },
			new PluralizerRule { Condition = new Regex("\\b((?:tit)?m|l)(?:ice|ouse)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1ice" },
			new PluralizerRule { Condition = new Regex("(pe)(?:rson|ople)$",RegexOptions.IgnoreCase),  ReplaceWith = "$1ople" },
			new PluralizerRule { Condition = new Regex("(child)(?:ren)?$",RegexOptions.IgnoreCase),  ReplaceWith = "$1ren" },
			new PluralizerRule { Condition = new Regex("eaux$",RegexOptions.IgnoreCase),  ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("m[ae]n$",RegexOptions.IgnoreCase), ReplaceWith = "men" },
			new PluralizerRule { Condition = new Regex("^thou$",RegexOptions.IgnoreCase), ReplaceWith = "you" },
			new PluralizerRule { Condition = new Regex("pox$",RegexOptions.IgnoreCase), ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("o[iu]s$",RegexOptions.IgnoreCase), ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("deer$",RegexOptions.IgnoreCase), ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("fish$",RegexOptions.IgnoreCase), ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("sheep$",RegexOptions.IgnoreCase), ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("measles$/",RegexOptions.IgnoreCase), ReplaceWith = "$0" },
			new PluralizerRule { Condition = new Regex("[^aeiou]ese$",RegexOptions.IgnoreCase), ReplaceWith = "$0" }
		};
	}

	public static ICollection<string> GetUncountables() {
		return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { 
			// Singular words with no plurals.
			"adulthood",
			"advice",
			"agenda",
			"aid",
			"aircraft",
			"alcohol",
			"ammo",
			"anime",
			"athletics",
			"audio",
			"bison",
			"blood",
			"bream",
			"buffalo",
			"butter",
			"carp",
			"cash",
			"chassis",
			"chess",
			"clothing",
			"cod",
			"commerce",
			"cooperation",
			"corps",
			"debris",
			"diabetes",
			"digestion",
			"elk",
			"energy",
			"equipment",
			"excretion",
			"expertise",
			"firmware",
			"flounder",
			"fun",
			"gallows",
			"garbage",
			"graffiti",
			"headquarters",
			"health",
			"herpes",
			"highjinks",
			"homework",
			"housework",
			"information",
			"jeans",
			"justice",
			"kudos",
			"labour",
			"literature",
			"machinery",
			"mackerel",
			"mail",
			"media",
			"mews",
			"moose",
			"music",
			"mud",
			"manga",
			"news",
			"only",
			"personnel",
			"pike",
			"plankton",
			"pliers",
			"police",
			"pollution",
			"premises",
			"rain",
			"research",
			"rice",
			"salmon",
			"scissors",
			"series",
			"sewage",
			"shambles",
			"shrimp",
			"software",
			"species",
			"staff",
			"swine",
			"tennis",
			"traffic",
			"transportation",
			"trout",
			"tuna",
			"wealth",
			"welfare",
			"whiting",
			"wildebeest",
			"wildlife",
			"you"
		};
	}

	private static Dictionary<string, string> IrregularWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
		// Pronouns.
		{"I", "we"},
		{"me", "us"},
		{"he", "they"},
		{"she", "they"},
		{"them", "them"},
		{"myself", "ourselves"},
		{"yourself", "yourselves"},
		{"itself", "themselves"},
		{"herself", "themselves"},
		{"himself", "themselves"},
		{"themself", "themselves"},
		{"is", "are"},
		{"was", "were"},
		{"has", "have"},
		{"this", "these"},
		{"that", "those"},
		// Words ending in with a consonant and `o`.
		{"echo", "echoes"},
		{"dingo", "dingoes"},
		{"volcano", "volcanoes"},
		{"tornado", "tornadoes"},
		{"torpedo", "torpedoes"},
		// Ends with `us`.
		{"genus", "genera"},
		{"viscus", "viscera"},
		// Ends with `ma`.
		{"stigma", "stigmata"},
		{"stoma", "stomata"},
		{"dogma", "dogmata"},
		{"lemma", "lemmata"},
		{"schema", "schemata"},
		{"anathema", "anathemata"},
		// Other irregular rules.
		{"ox", "oxen"},
		{"axe", "axes"},
		{"die", "dice"},
		{"yes", "yeses"},
		{"foot", "feet"},
		{"eave", "eaves"},
		{"goose", "geese"},
		{"tooth", "teeth"},
		{"quiz", "quizzes"},
		{"human", "humans"},
		{"proof", "proofs"},
		{"carve", "carves"},
		{"valve", "valves"},
		{"looey", "looies"},
		{"thief", "thieves"},
		{"groove", "grooves"},
		{"pickaxe", "pickaxes"},
		{"passerby","passersby" },
		{"cookie","cookies" }
	};

	public static string GetLastCapitalizedWord(string s) {
		if (string.IsNullOrEmpty(s))
			return s;
		// Add space before uppercase.
		var ucRx = new Regex("([A-Z][^A-Z])", RegexOptions.Multiline);
		s = ucRx.Replace(s, " $1").Trim();
		// Replace with spaces.
		var spRx = new Regex("[_ ]+", RegexOptions.Multiline);
		s = spRx.Replace(s, " ").Trim().Split(' ').Last();
		return s;
	}

	public static IDictionary<string, string> GetIrregularPlurals() {
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, string> item in IrregularWords.Reverse())
			if (!result.ContainsKey(item.Value)) result.Add(item.Value, item.Key);
		return result;
	}

	public static IDictionary<string, string> GetIrregularSingulars() {
		return IrregularWords;
	}

}

#endregion

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

//----END T4 CODE-----
// #>
