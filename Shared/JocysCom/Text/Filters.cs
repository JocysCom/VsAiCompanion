using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JocysCom.ClassLibrary.Text
{
    /// <summary>
    /// Provides text normalization and filtering operations for HTML stripping, key generation, name and title formatting.
    /// </summary>
    public partial class Filters
    {

        // Used for author full name.
        private readonly char[] NameChars = new char[] { '"' };
        private readonly char[] BasicChars = new char[] { '-', ' ', ',', '\u00A0' };
        public TextInfo Culture = new CultureInfo("en-US", false).TextInfo;
        private readonly Regex r22 = new Regex("\"");

        // Multiple single quotes
        private readonly Regex rmq = new Regex("'[']+");
        private readonly Regex r27 = new Regex("'");
        private readonly Regex r2E = new Regex("\\.");
        private readonly Regex rL2E = new Regex("([A-Z])\\.");
        private readonly Regex rU1 = new Regex("^([A-Z])\\s+");
        private readonly Regex rU2 = new Regex("\\s+([A-Z])\\s+");
        private readonly Regex rU3 = new Regex("\\s+([A-Z])$");
        private readonly Regex rAnd = new Regex("\\s*&\\s*");
        public static readonly Regex RxAllExceptNumbers = new Regex("[^0-9]", RegexOptions.IgnoreCase);
        public static readonly Regex RxAllExceptDecimal = new Regex("[^0-9.]", RegexOptions.IgnoreCase);
        public static readonly Regex RxAllExceptLetters = new Regex("[^A-Z]", RegexOptions.IgnoreCase);
        public static readonly Regex RxAllExceptLettersAndSpaces = new Regex("[^A-Z ]", RegexOptions.IgnoreCase);
        public static readonly Regex RxAllExceptNumbersAndLetters = new Regex("/^[a-zA-Z0-9]", RegexOptions.IgnoreCase);
        public static readonly Regex RxAllExceptNumbersLettersAndSpaces = new Regex("/^[a-zA-Z0-9 \r\n]", RegexOptions.IgnoreCase);
        public static readonly Regex RxNumbersOnly = new Regex("[0-9]", RegexOptions.IgnoreCase);
        public static readonly Regex RxLettersOnly = new Regex("[A-Z]", RegexOptions.IgnoreCase);
        public static readonly Regex RxBreaks = new Regex("[\r\n]", RegexOptions.Multiline);
        public static readonly Regex RxUppercase = new Regex("(\\W)", RegexOptions.Multiline);
        public static readonly Regex RxMultiSpace = new Regex("[ \u00A0]+");
        public static readonly Regex RxAllowedInKey = new Regex("[^A-Z0-9 ]+", RegexOptions.IgnoreCase);
        // http://www.rfc-editor.org/rfc/rfc1738.txt
        public static readonly Regex RxAllowedInUrl = new Regex("[^A-Z0-9$-_.+!*'() ]+", RegexOptions.IgnoreCase);
        public static readonly Regex RxTheAtStart = new Regex("^The\\s+", RegexOptions.IgnoreCase);
        public static readonly Regex RxTheAtEnd = new Regex("[,][\\s]*The$", RegexOptions.IgnoreCase);
        public static readonly Regex RxAAtStart = new Regex("^A\\s+", RegexOptions.IgnoreCase);
        public static readonly Regex RxAAtEnd = new Regex("[,][\\s]*A$", RegexOptions.IgnoreCase);
        public static readonly Regex RxGuid = new Regex(
            "^[a-f0-9]{32}$|" +
            "^({|\\()?[a-f0-9]{8}-([a-f0-9]{4}-){3}[a-f0-9]{12}(}|\\))?$|" +
            "^({)?[0xa-f0-9]{3,10}(, {0,1}[0xa-f0-9]{3,6}){2}, {0,1}({)([0xa-f0-9]{3,4}, {0,1}){7}[0xa-f0-9]{3,4}(}})$", RegexOptions.IgnoreCase);
        public static readonly Regex RxGuidContains = new Regex(
            "[a-f0-9]{32}|" +
            "({|\\()?[a-f0-9]{8}-([a-f0-9]{4}-){3}[a-f0-9]{12}(}|\\))?|" +
            "({)?[0xa-f0-9]{3,10}(, {0,1}[0xa-f0-9]{3,6}){2}, {0,1}({)([0xa-f0-9]{3,4}, {0,1}){7}[0xa-f0-9]{3,4}(}})", RegexOptions.IgnoreCase);

		// This expression will: find and replace all tags with nothing and avoid problematic edge cases.
		// <(?:[^>=]|='[^']*'|="[^"]*"|=[^'"][^\s>]*)*>
		public static readonly Regex RxHtmlTag = new Regex(@"<(?:[^>=]|='[^']*'|=""[^""]*""|=[^'""][^\s>]*)*>", RegexOptions.IgnoreCase | RegexOptions.Multiline);
		//public static readonly Regex RxHtmlTag = new Regex(@"<(.|\n)*?>", RegexOptions.IgnoreCase | RegexOptions.Multiline);

		public const int CropTextDefauldMaxLength = 128;

        /// <summary>
        /// Truncates the string representation to a maximum length, optionally removing HTML.
        /// Returns empty if maxLength is -1, full text if maxLength is 0, or truncated text with an ellipsis.
        /// </summary>
        /// <param name="so">The object whose string representation is cropped.</param>
        /// <param name="maxLength">Maximum allowed length: -1 yields empty string, 0 yields full text, null defaults to 128.</param>
        /// <param name="stripHtml">If true, remove HTML tags before cropping.</param>
        /// <returns>Cropped string with ellipsis if truncated, or original/empty based on maxLength.</returns>
        public static string CropText(object so, int? maxLength = 0, bool stripHtml = true)
        {
            var s = string.Format("{0}", so);
            if (!maxLength.HasValue)
                maxLength = CropTextDefauldMaxLength;
            if (string.IsNullOrEmpty(s) || maxLength == -1)
                return string.Empty;
            if (maxLength == 0)
                return s;
            if (maxLength == 0) maxLength = CropTextDefauldMaxLength;
            if (stripHtml) s = StripHtml(s);
            if (s.Length > maxLength)
            {
                s = s.Substring(0, maxLength.Value - 3);
                // Find last separator and crop there...
                var ls = s.LastIndexOf(' ');
                if (ls > 0) s = s.Substring(0, ls);
                s += "...";
            }
            return s;
        }

        /// <summary>
        /// Removes HTML tags from the string, replaces tabs with spaces, collapses whitespace, and trims.
        /// </summary>
        /// <param name="s">Input HTML string.</param>
        /// <returns>Plain text without HTML markup and normalized whitespace.</returns>
        public static string StripHtml(string s)
        {
            return StripHtml(s, false);
        }

        /// <summary>
        /// Removes HTML tags, replaces tabs, optionally replaces line breaks, collapses whitespace, and trims.
        /// </summary>
        /// <param name="s">Input HTML string.</param>
        /// <param name="removeBreaks">True to replace line breaks with spaces.</param>
        /// <returns>Sanitized text without HTML markup and normalized whitespace.</returns>
        public static string StripHtml(string s, bool removeBreaks)
        {
            s = RxHtmlTag.Replace(s, string.Empty);
            s = s.Replace("\t", " ");
            if (removeBreaks) s = RxBreaks.Replace(s, " ");
            // Replace multiple spaces.
            s = RxMultiSpace.Replace(s, " ");
            return s.Trim();
        }

        /// <summary>
        /// Removes unsafe HTML tags, preserving only allowed whitelist tags.
        /// </summary>
        /// <param name="s">Input HTML string.</param>
        /// <param name="whiteList">Optional array of allowed tag names; defaults to common formatting tags.</param>
        /// <returns>Text with only safe HTML tags or plain text if no whitelist specified.</returns>
        public static string StripUnsafeHtml(string s, string[] whiteList = null)
        {
            var acceptable = whiteList is null
                ? "i|em|b|strong|u|sup|sub|ol|ul|li|br|h2|h3|h4|h5|span|div|p|a|img|blockquote"
                : string.Join("|", whiteList);
            var stringPattern = @"<\/?(?(?=" + acceptable + @")notag|[a-z0-9]+:?[a-z0-9]+?)(?:\s[a-z0-9\-]+=?(?:(["",']?).*?\1?)?)*\s*\/?>";
            return Regex.Replace(s, stringPattern, "", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

		private readonly List<string> Honors;
		private readonly List<string> Prefixes;
		private readonly List<string> Prefixes2;
		private readonly List<string> Sufixes;

		/// <summary>
		/// Initializes filter lists for honors, prefixes, and suffixes used in name processing.
		/// </summary>
		public Filters()
        {
            //http://www.luciehaskins.com/resources/recnamen.pdf
            Honors = FillList("Dr", "PhD");
            Prefixes = FillList("De", "Du", "van", "der", "von",
            "den", "op", "ter", "ten", "van't", "van", "den", "und", "zu");
            // Prefixes which requires two words in name.
            Prefixes2 = FillList("Al");
            Sufixes = FillList(new string[] { });
        }

        private List<string> FillList(params string[] values)
        {
            var list = new List<string>();
            for (var i = 0; i < values.Length; i++)
            {
                list.Add(values[i].ToUpper());
            }
            return list;
        }

        private string[] FilterList(string[] list, List<string> filter)
        {
            var newList = new List<string>();
            foreach (var item in list)
            {
                if (!filter.Contains(item)) newList.Add(item);
            }
            return newList.ToArray();
        }

        private List<string> GetSingleChar(string[] list)
        {
            var newList = new List<string>();
            foreach (var item in list)
            {
                if (item.Length == 1) newList.Add(item);
            }
            return newList;
        }

        private List<string> GetMultiChar(string[] list)
        {
            var newList = new List<string>();
            foreach (var item in list)
            {
                if (item.Length > 1) newList.Add(item);
            }
            return newList;
        }

        private char[] GetDistinct(char[] list)
        {
            var newList = new List<char>();
            foreach (var item in list)
            {
                if (!newList.Contains(item)) newList.Add(item);
            }
            return newList.ToArray();
        }

        private string[] GetUnicodeEscaped(char[] list)
        {
            var newList = new List<string>();
            foreach (var item in list)
            {
                newList.Add("\\u" + ((uint)item).ToString("X4"));
            }
            return newList.ToArray();
        }

        #region Filter: Person Name

        /// <summary>
        /// Normalizes a full name by basic filtering, trimming quotes, removing dots, title-casing, and abbreviating initials.
        /// </summary>
        /// <param name="s">Raw full name.</param>
        /// <returns>Formatted name with initials abbreviated and proper casing.</returns>
        public string GetPersonName(string s)
        {
            s = FilterBasic(s);
            // Trim corner chars.
            s = s.Trim(NameChars);
            // Remove all dots.
            s = r2E.Replace(s, string.Empty);
            //-------------------------------------------------
            // Capitalize.
            s = Culture.ToTitleCase(s);
            //-------------------------------------------------
            // Convert: A B C Surname  => A. B. C. Surname
            //-------------------------------------------------
            s = rU1.Replace(s, "$1. ");
            s = rU2.Replace(s, " $1. ");
            s = rU3.Replace(s, " $1.");
            return s;
        }

        /// <summary>
        /// Generates a sortable key for a person's name: formats name, prepares it for key, optionally sorts components,
        /// removes honors, prefixes, suffixes, then abbreviates initials.
        /// </summary>
        /// <param name="s">Raw full name.</param>
        /// <param name="sortName">If true, sorts name parts alphabetically before filtering.</param>
        /// <returns>Name key with initials abbreviated and non-essential parts removed.</returns>
        public string GetPersonNameKey(string s, bool sortName)
        {
            //-------------------------------------------------
            s = GetPersonName(s);
            s = GetKeyPrepare(s);
            //-------------------------------------------------
            var sa = s.Split(' ');
            if (sortName) Array.Sort(sa);
            //-------------------------------------------------
            // Remove honors.
            sa = FilterList(sa, Honors);
            // Remove prefixes.
            sa = FilterList(sa, Prefixes);
            // Remove prefixes like "AL" in arab names.
            if (sa.Length > 1) sa = FilterList(sa, Prefixes2);
            // Remove sufixes.
            sa = FilterList(sa, Sufixes);
            for (var i = 0; i < sa.Length; i++)
            {
                // fix spelling mistakes, accents. replace most commmon mistakes.
            }
            // Shorten all forenames one char. Keep surename A B C Surname.
            // Expand single letters to possible name ???
            //-------------------------------------------------
            // convert: B A C Surname => A B C Surname
            //-------------------------------------------------
            // Get all single char words.
            var sWords = GetSingleChar(sa);
            // Get all multiple char words.
            var mWords = GetMultiChar(sa);
            // Create new name.
            for (var i = 0; i < mWords.Count; i++) sWords.Add(mWords[i]);
            s = string.Join(" ", sWords.ToArray());
            //-------------------------------------------------
            // Convert: A B C Surname => A. B. C. Surname
            //-------------------------------------------------
            s = rU1.Replace(s, "$1. ");
            s = rU2.Replace(s, " $1. ");
            s = rU3.Replace(s, " $1.");
            return s;
        }

        /// <summary>
        /// Appends ONIX metadata suffixes (birth/death date, key name suffix, title, contributor role) to a name key.
        /// </summary>
        /// <param name="s">Base name key.</param>
        /// <param name="ICCY">Date of birth/death code.</param>
        /// <param name="ICKNS">Key name suffix (e.g., Jr, III).</param>
        /// <param name="ICTAN">Title displayed after name.</param>
        /// <param name="CRC">ONIX contributor role code.</param>
        /// <returns>Name key appended with formatted suffix metadata.</returns>
        public string AppendNameKeySufix(string s, string ICCY, string ICKNS, string ICTAN, string CRC)
        {
            //ICCY Date of birth or date of birth and death
            //ICKNS Key name suffix e.g. Jr, III
            //ICTAN Title as displayed after name
            //CR The ONIX role this contributor takes – Code (from ONIX list 17)
            var sufix = string.Format("{0} {1} {2} {3}", ICCY, ICKNS, ICTAN, CRC);
            sufix = GetKeyPrepare(sufix);
            s = string.Format("{0} {1}", s, sufix).Trim();
            return s;
        }

        #endregion

        #region Filter: Title

        private readonly Regex r2E3 = new Regex("\\.\\s+\\.\\s+\\.");

        /// <summary>
        /// Applies basic filtering to a title string.
        /// </summary>
        /// <param name="s">Raw title string.</param>
        /// <returns>Filtered title.</returns>
        public string GetTitle(string s)
        {
            s = FilterBasic(s);
            // Merge dots.
            //s = r2E3.Replace(s,"...");
            return s;
        }

        /// <summary>
        /// Generates a sortable key for a title: prepares text, replaces &apos;&amp;&apos; with &apos;AND&apos;,
        /// optionally strips leading/trailing &apos;The&apos; or &apos;A&apos;, normalizes spaces, and trims.
        /// </summary>
        /// <param name="s">Raw title string.</param>
        /// <param name="stripThe">Remove leading/trailing 'The' if true.</param>
        /// <param name="stripA">Remove leading/trailing 'A' if true.</param>
        /// <returns>Title key prepared for sorting or indexing.</returns>
        public string GetTitleKey(string s, bool stripThe = false, bool stripA = false)
        {
            var result = GetKeyPrepare(s);
            // Replace '&' with 'AND'
            result = rAnd.Replace(result, " AND ");
            if (stripThe)
            {
                // Remove 'The' from start and end.
                result = RxTheAtStart.Replace(result, string.Empty);
                result = RxTheAtEnd.Replace(result, string.Empty);
            }
            if (stripA)
            {
                // Remove 'A' from start and end.
                result = RxAAtStart.Replace(result, string.Empty);
                result = RxAAtEnd.Replace(result, string.Empty);
            }
            // Replace multiple spaces.
            s = RxMultiSpace.Replace(s, " ");
            // Trim basic chars.
            s = s.Trim(BasicChars);
            return result;
        }

        #endregion


        /// <summary>
        /// Remove diacritic marks (accent marks) from characters and convert to ASCII.
        /// "Fuerza Aérea (Edificio Cóndor) Heliport" -> "Fuerza Aerea (Edificio Condor) Heliport"
        /// </summary>
        /// <param name="input">The string to process.</param>
        public static string ConvertToASCII(string input)
        {
            var stFormD = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            for (var ich = 0; ich < stFormD.Length; ich++)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(stFormD[ich]);
                }
            }
            var text = sb.ToString().Normalize(NormalizationForm.FormC);
            // Remove rest chars.
            text = FoldToASCII(text);
            return text;
        }

        /// <summary>
        /// Remove multi-chars so authors PIGGOT and PIGOT on same book can be identified as same author.
        /// </summary>
        /// <param name="s">Input string to collapse character runs.</param>
        /// <returns>String with runs of repeating characters truncated to single occurrences.</returns>
        /// <remarks>This method is case sensitive, but key name short is ALL UPPER CASE.</remarks>
        public string RemoveMultiChars(string s)
        {
            return RemoveMultiChars(s, s.ToCharArray(), 1);
        }

        /// <summary>
        /// Get Regular expression pattern from string. All chars will be converted to \uNNNN form.
        /// </summary>
        /// <param name="s">String to convert</param>
        /// <returns>Regular expression pattern</returns>
        public string GetEscapedPattern(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Get array of \uNNNN strings
            var us = GetUnicodeEscaped(s.ToCharArray());
            // Join into one string and return.
            return string.Join("", us);
        }

        public string RemoveMultiChars(string s, char[] chars, int max)
        {
            // This is needed for regular expression because s can contain specials chars like '.', '?'
            var c = GetDistinct(chars);
            var result = s;
            for (var i = 0; i < c.Length; i++) result = RemoveMultiChars(result, c[i].ToString(), max);
            return result;
        }

        public string RemoveMultiChars(string s, string word, int max)
        {
            var uword = GetEscapedPattern(word);
            var sword = string.Empty;
            for (var i = 0; i < max; i++) sword += word;
            return System.Text.RegularExpressions.Regex.Replace(s, "((" + uword + "){" + max + ",})", sword);
        }

        /// <summary>
        /// Inserts spaces before non-word characters and collapses multiple spaces.
        /// </summary>
        /// <param name="s">Input string.</param>
        /// <returns>String with spaces added before punctuation and normalized spacing.</returns>
        public static string AddSpaceBeforeUppercase(string s)
        {
            s = RxUppercase.Replace(s, " $1");
            // Replace multiple spaces.
            s = RxMultiSpace.Replace(s, " ").Trim();
            return s;
        }

        /// <summary>
        /// Performs basic normalization: removes line breaks, ensures space after periods, trims basic characters, collapses spaces, and normalizes quotes.
        /// </summary>
        /// <param name="s">Input string to normalize.</param>
        /// <returns>Normalized string with consistent spacing and quotes.</returns>
        public string FilterBasic(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // Remove line breaks.
            s = RxBreaks.Replace(s, string.Empty);
            // Add space after letter dot.
            s = rL2E.Replace(s, "$1. ");
            // Trim basic chars.
            s = s.Trim(BasicChars);
            // Replace multiple spaces.
            s = RxMultiSpace.Replace(s, " ");
            // Replace multiple single quotes with double quotes.
            s = rmq.Replace(s, "\"");
			// Remove outside quotes.
			if (r22.Matches(s).Count == 1) s = s.Trim('"');
            if (r22.Matches(s).Count == 2) if (s.StartsWith("\"") && s.EndsWith("\"")) s = r22.Replace(s, string.Empty);
			if (r27.Matches(s).Count == 1) s = s.Trim('\'');
			if (r27.Matches(s).Count == 2) if (s.StartsWith("'") && s.EndsWith("'")) s = r27.Replace(s, string.Empty);
			// Trim basic chars again.
			s = s.Trim(BasicChars);
            return s;
        }

        /// <summary>
        /// Prepares a string for key generation: removes diacritics, converts to uppercase, replaces disallowed characters with spaces, collapses spaces, and trims.
        /// </summary>
        /// <param name="s">String to prepare.</param>
        /// <returns>Cleaned uppercase string ready for key formation.</returns>
        public string GetKeyPrepare(string s)
        {
            // Filter accents: Hélan => Helan
            s = ConvertToASCII(s);
            // Convert to upper-case and replace non allowed chars with space.
            s = RxAllowedInKey.Replace(s.ToUpper(), " ");
            // Replace multiple spaces.
            s = RxMultiSpace.Replace(s, " ");
            // Trim basic chars.
            s = s.Trim(BasicChars);
            return s;
        }

        public static string ToTitleCase(string input)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input);
        }

        /// <summary>
        /// Convert text to ASCII key.
        /// "Fuerza Aérea (Edificio Cóndor) Heliport" -> "Fuerza_Aerea_Edificio_Condor_Heliport"
        /// </summary>
        /// <param name="input">The string to convert.</param>
        /// <param name="capitalize">If true, applies title casing after key preparation.</param>
        /// <returns>ASCII key string.</returns>
        public static string GetKey(string input, bool capitalize)
        {
            // Filter accents: Hélan => Helan
            var s = ConvertToASCII(input);
            // Convert to upper-case and keep only allowed chars.
            s = RxAllowedInKey.Replace(s, " ");
            // Replace multiple spaces.
            s = RxMultiSpace.Replace(s, " ").Trim();
            if (capitalize)
            {
                var filters = new Filters();
                s = filters.GetKeyPrepare(s).ToLower();
                s = filters.Culture.ToTitleCase(s);
            }
            s = s.Replace(' ', '_');
            return s;
        }

        /// <summary>
        /// Regex replacement evaluator: rebuilds match with third group uppercased.
        /// </summary>
        /// <param name="m">Regex match.</param>
        /// <returns>Concatenated string with last group in uppercase.</returns>
        public string ReplaceM(Match m)
        {
            var S1 = m.Groups[1].Value;
            var S2 = m.Groups[2].Value;
            var S3 = m.Groups[3].Value;
            return S1 + S2 + S3.ToUpper();
        }

    }
}
