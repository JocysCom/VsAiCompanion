using System;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace JocysCom.ClassLibrary.Files
{
	public static class CsvHelper
	{
		public static string Write(DataTable table, bool header = true, string delimeter = ",", CsvQuote quoteType = CsvQuote.Auto)
		{
			var ms = new MemoryStream();
			var sw = new StreamWriter(ms, System.Text.Encoding.Unicode);
			Write(sw, table, header, delimeter, quoteType);
			var s = System.Text.Encoding.Unicode.GetString(ms.ToArray());
			sw.Dispose();
			return s;
		}

		public static void Write(string name, DataTable table, bool header, string delimeter, CsvQuote quoteType)
		{
			var fs = new FileStream(name, FileMode.CreateNew);
			var sw = new StreamWriter(fs);
			Write(sw, table, header, delimeter, quoteType);
			sw.Dispose();
		}

		public static void Write(StreamWriter stream, DataTable table, bool header, string delimeter, CsvQuote quoteType)
		{
			if (stream is null)
				throw new ArgumentNullException(nameof(stream));
			if (table is null)
				throw new ArgumentNullException(nameof(table));
			// Write headers.
			if (header)
				WriteColumn(stream, table.Columns, delimeter, quoteType);
			// Write data.
			foreach (DataRow row in table.Rows)
				WriteRow(stream, row, delimeter, quoteType);
			stream.Flush();
		}

		public static void WriteColumn(TextWriter stream, DataColumnCollection columns, string delimeter, CsvQuote quoteType)
		{
			var count = columns.Count;
			for (var i = 0; i < count; i++)
			{
				var column = columns[i];
				WriteValue(stream, column.Caption, delimeter, quoteType);
				if (i < count - 1)
					stream.Write(delimeter);
				else
					stream.Write(Environment.NewLine);
			}
			stream.Flush();
		}

		public static void WriteRow(TextWriter stream, DataRow row, string delimeter, CsvQuote quoteType)
		{
			var count = row.Table.Columns.Count;
			for (var i = 0; i < count; i++)
			{
				WriteValue(stream, row[i], delimeter, quoteType);
				if (i < count - 1)
					stream.Write(delimeter);
				else
					stream.Write(Environment.NewLine);
			}
		}

		private static void WriteValue(TextWriter stream, object value, string delimeter, CsvQuote quoteType)
		{
			if (value is null)
				return;
			var s = value.ToString().Trim();
			var quote = quoteType == CsvQuote.All ||
				(value is string && quoteType == CsvQuote.Strings) ||
				s.IndexOfAny((delimeter + "\"\x0A\x0D").ToCharArray()) > -1;
			if (quote)
				stream.Write("\"" + s.Replace("\"", "\"\"") + "\"");
			else
				stream.Write(s);
		}

#if NETCOREAPP // .NET Core
#elif NETSTANDARD // .NET Standard
#else // .NET Framework

		public static DataTable ReadWithOleDb(string path, bool haveHeader)
		{
			var header = haveHeader ? "Yes" : "No";
			var pathOnly = Path.GetDirectoryName(path);
			var fileName = Path.GetFileName(path);
			var sql = @"SELECT * FROM [" + fileName + "]";
			using (var connection = new OleDbConnection(
					  @"Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + pathOnly +
					  ";Extended Properties=\"Text;HDR=" + header + "\""))
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
			using (var command = new OleDbCommand(sql, connection))
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
			{
				using (var adapter = new OleDbDataAdapter(command))
				{
					var table = new DataTable();
					table.Locale = CultureInfo.CurrentCulture;
					adapter.Fill(table);
					return table;
				}
			}
		}

#endif

		public static DataTable Read(string path, bool haveHead = false, bool readData = true)
		{
			var table = new DataTable();
			Read(path, (position, line, values) =>
			{
				// If CSV have column header then...
				if (haveHead && line == 0)
				{
					for (int i = 0; i < values.Length; i++)
						table.Columns.Add(values[i]);
					if (!readData)
						return false;
				}
				else
				{
					var row = table.NewRow();
					row.ItemArray = values;
					table.Rows.Add(row);
				}
				return true;
			});
			return table;
		}

		static Regex splitRx
		{
			get
			{
				if (_splitRx is null)
				{
					// https://stackoverflow.com/questions/34132392/regular-expression-c-for-csv-by-rfc-4180
					_splitRx = new Regex(
					@"(?<=\r|\n|^)(?!\r|\n|$)" +
					@"(?:" +
						@"(?:" +
							@"""(?<Value>(?:[^""]|"""")*)""|" +
							@"(?<Value>(?!"")[^,\r\n]+)|" +
							@"""(?<OpenValue>(?:[^""]|"""")*)(?=\r|\n|$)|" +
							@"(?<Value>)" +
						@")" +
						@"(?:,|(?=\r|\n|$))" +
					@")+?" +
					@"(?:(?<=,)(?<Value>))?" +
					@"(?:\r\n|\r|\n|$)",
					RegexOptions.Compiled);
				}
				return _splitRx;
			}
		}
		static Regex _splitRx;

		public static void Read(string path, Func<long, int, string[], bool> callBack)
		{

			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var sr = new StreamReader(fs))
			{
				string line = null;
				var i = 0;
				var allowToContinue = true;
				while (allowToContinue && (line = sr.ReadLine()) != null)
				{
					var matches = splitRx.Matches(line).Cast<Match>().ToArray();
					// In this case Length will be always 1.
					for (var m = 0; m < matches.Length; m++)
					{
						var match = matches[m];
						var columns = match.Groups["Value"].Captures.Count;
						var values = new string[columns];
						for (var c = 0; c < columns; c++)
						{
							var capture = match.Groups["Value"].Captures[c];
							//Console.Write("R" + (m + 1) + ":V" + (c + 1) + " = ");
							var isEmptyOrNotQuoted = capture.Length == 0 || capture.Index == match.Index || match.Value[capture.Index - match.Index - 1] != '\"';
							values[c] = isEmptyOrNotQuoted
								? capture.Value
								: capture.Value.Replace("\"\"", "\"");
						}
						//foreach (var openValue in Record.Groups["OpenValue"].Captures)
						//	Console.WriteLine("ERROR - Open ended quoted value: " + openValue.Value);
						allowToContinue = callBack.Invoke(sr.BaseStream.Position, i, values);
						if (!allowToContinue)
							break;
					}
					i++;
				}
			}
		}
	}
}
