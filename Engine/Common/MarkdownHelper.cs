using System;
using System.Collections.Generic;
using System.IO;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class MarkdownHelper
	{
		public static string MarkdownLanguageNames { get; set; } =
			"ABAP,ABNF,AL,ANTLR4,APL,AQL,ARFF,ARMASM,ASM6502,AWK,ActionScript,Ada,Agda,ApacheConf,Apex," +
			"AppleScript,Arduino,Arturo,Asciidoc,Asmatmel,Aspnet,AutoHotkey,AutoIt,AviSynth,Avro-IDL," +
			"BBCode,BBJ,BNF,BQN,BSL,Bash,Basic,Batch,Bicep,Birb,Bison,Brainfuck,BrightScript,Bro," +
			"C,CFScript,CIL,CMake,COBOL,CSHTML,CSP,CSS,CSS-Extras,CSV,Chaiscript,CilkC,CilkCpp,Clike,Clojure,CoffeeScript,Concurnas,Cooklang,Coq,Cpp,Crystal,Csharp,Cue,Cypher," +
			"D,DAX,DNS-Zone-File,Dart,DataWeave,Dhall,Diff,Django,Docker,Dot," +
			"EBNF,EJS,ERB,EditorConfig,Eiffel,Elixir,Elm,Erlang,Excel-Formula," +
			"FORTRAN,FTL,Factor,False,Firestore-Security-Rules,Flow,Fsharp," +
			"GAP,GCode,GEDCOM,GLSL,GML,GN,GdScript,Gettext,Gherkin,Git,Go,Go-Module,Gradle,Graphql,Groovy," +
			"HCL,HLSL,HPKP,HSTS,HTTP,Haml,Handlebars,Haskell,Haxe,Hoon," +
			"ICU-Message-Format,IECSt,INI,IchigoJam,Icon,Idris,Ignore,Inform7,Io," +
			"J,JEXL,JQ,JS-Extras,JS-Templates,JSDoc,JSON,JSON5,JSONP,JSStackTrace,JSX," +
			"Java,JavaDoc,JavaDocLike,JavaScript,JavaStackTrace,Jolie,Julia," +
			"KeepALIVED,Keyman,Kotlin,Kumir,Kusto," +
			"LLVM,LOLCode,Latex,Latte,Less,LilyPond,Linker-Script,Liquid,Lisp,LiveScript,Log,Lua," +
			"MAXScript,MEL,Magma,Makefile,Markdown,Markup,Markup-Templating,Mata,Matlab,Mermaid,MetaFont,Mizar,MongoDB,Monkey,MoonScript," +
			"N1QL,N4JS,NASM,NSIS,Nand2Tetris-HDL,Naniscript,Neon,Nevod,Nginx,Nim,Nix," +
			"OCaml,ObjectiveC,Odin,OpenCL,OpenQASM,Oz," +
			"PCAxis,PHP,PHP-Extras,PHPDoc,PLSQL,PSL,PariGP,Parser,Pascal,PascalIGO,PeopleCode,Perl,Plant-UML,PowerQuery," +
			"PowerShell,Processing,Prolog,PromQL,Properties,Protobuf,Pug,Puppet,Pure,PureBasic,PureScript,Python," +
			"Q,QML,QSharp,Qore," +
			"R,REST,RIP,Racket,Reason,Regex,Rego,RenPY,Rescript,Roboconf,RobotFramework,Ruby,Rust," +
			"SAS,SCSS,SML,SPARQL,SQF,SQL,Sass,Scala,Scheme,Shell-Session,Smali,SmallTalk,Smarty,Solidity," +
			"Solution-File,Soy,Splunk-SPL,Squirrel,Stan,Stata,Stylus,SuperCollider,Swift,Systemd," +
			"T4-CS,T4-Templating,T4-VB,TAP,TOML,TSX,TT2,Tcl,Textile,Tremor,Turtle,Twig,TypeScript,TypoScript," +
			"UORazor,URI,UnrealScript," +
			"V,VBNet,VHDL,Vala,Velocity,Verilog,Vim,Visual-Basic," +
			"WASM,WGSL,WarpScript,Web-IDL,Wiki,Wolfram,Wren," +
			"XML,XQuery,Xeora,Xojo," +
			"YAML,Yang,ZigetLua";

		public static string GetMarkdownLanguage(string filePath, string mimeType = null)
		{
			var extension = Path.GetExtension(filePath);
			var language = ExtensionToLanguageMap.ContainsKey(extension)
				? ExtensionToLanguageMap[extension]
				: string.IsNullOrEmpty(extension) ? "" : extension.Substring(1);
			return language;
		}

		public static string CreateMarkdownCodeBlock(string contents, string language)
		{
			// Find the maximum number of consecutive backticks in the content
			var maxBackticksInContent = GetMaxConsecutiveCharCount(contents, '`');
			// Determine the number of backticks to use in the code fence
			var backtickCount = Math.Max(3, maxBackticksInContent + 1);
			var backticks = new string('`', backtickCount);
			return $"{backticks}{language}\n{contents}\n{backticks}";
		}


		public static string CreateMarkdownCodeBlock(string filePath, string fileContent, string mimeType)
		{
			var language = GetMarkdownLanguage(filePath, mimeType);
			return CreateMarkdownCodeBlock(fileContent, language);
		}

		private static int GetMaxConsecutiveCharCount(string input, char charToCount)
		{
			int maxCount = 0;
			int currentCount = 0;
			foreach (char c in input)
			{
				if (c == charToCount)
				{
					currentCount++;
					if (currentCount > maxCount)
						maxCount = currentCount;
				}
				else
				{
					currentCount = 0;
				}
			}
			return maxCount;
		}

		public static readonly Dictionary<string, string> ExtensionToLanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ ".abap", "ABAP" },
			{ ".abnf", "ABNF" },
			{ ".ada", "Ada" },
			{ ".adb", "Ada" },
			{ ".ado", "Stata" },
			{ ".adoc", "Asciidoc" },
			{ ".ads", "Ada" },
			{ ".agda", "Agda" },
			{ ".ahk", "AutoHotkey" },
			{ ".al", "AL" },
			{ ".apl", "APL" },
			{ ".applescript", "AppleScript" },
			{ ".aql", "AQL" },
			{ ".arff", "ARFF" },
			{ ".art", "Arturo" },
			{ ".as", "ActionScript" },
			{ ".ascx", "Aspnet" },
			{ ".asm", "ARMASM" },
			{ ".asp", "Aspnet" },
			{ ".aspx", "Aspnet" },
			{ ".au3", "AutoIt" },
			{ ".avdl", "Avro-IDL" },
			{ ".avs", "AviSynth" },
			{ ".awk", "AWK" },
			{ ".bas", "Basic" },
			{ ".bash", "Bash" },
			{ ".bat", "Batch" },
			{ ".bbj", "BBJ" },
			{ ".bf", "Brainfuck" },
			{ ".bi", "Basic" },
			{ ".bicep", "Bicep" },
			{ ".birb", "Birb" },
			{ ".bnf", "BNF" },
			{ ".bqn", "BQN" },
			{ ".bro", "Bro" },
			{ ".brs", "BrightScript" },
			{ ".bsl", "BSL" },
			{ ".c", "C" },
			{ ".cbl", "COBOL" },
			{ ".cc", "Cpp" },
			{ ".cfc", "CFScript" },
			{ ".cfm", "CFScript" },
			{ ".chai", "Chaiscript" },
			{ ".cilk", "CilkC" },
			{ ".cl", "OpenCL" },
			{ ".clj", "Clojure" },
			{ ".cljc", "Clojure" },
			{ ".cljs", "Clojure" },
			{ ".cls", "Apex" },
			{ ".cmake", "CMake" },
			{ ".cmd", "Batch" },
			{ ".cob", "COBOL" },
			{ ".coffee", "CoffeeScript" },
			{ ".conc", "Concurnas" },
			{ ".conf", "ApacheConf" },
			{ ".cook", "Cooklang" },
			{ ".cpp", "Cpp" },
			{ ".cpy", "COBOL" },
			{ ".cr", "Crystal" },
			{ ".cs", "Csharp" },
			{ ".cshtml", "CSHTML" },
			{ ".csp", "CSP" },
			{ ".css", "CSS" },
			{ ".csv", "CSV" },
			{ ".cue", "Cue" },
			{ ".cxx", "Cpp" },
			{ ".cyp", "Cypher" },
			{ ".cypher", "Cypher" },
			{ ".d", "D" },
			{ ".dart", "Dart" },
			{ ".dax", "DAX" },
			{ ".dhall", "Dhall" },
			{ ".diff", "Diff" },
			{ ".djhtml", "Django" },
			{ ".do", "Stata" },
			{ ".dockerfile", "Docker" },
			{ ".dot", "Dot" },
			{ ".dwl", "DataWeave" },
			{ ".e", "Eiffel" },
			{ ".ebnf", "EBNF" },
			{ ".editorconfig", "EditorConfig" },
			{ ".ejs", "EJS" },
			{ ".elm", "Elm" },
			{ ".erb", "ERB" },
			{ ".erl", "Erlang" },
			{ ".ex", "Elixir" },
			{ ".exs", "Elixir" },
			{ ".f03", "FORTRAN" },
			{ ".f90", "FORTRAN" },
			{ ".f95", "FORTRAN" },
			{ ".factor", "Factor" },
			{ ".false", "False" },
			{ ".feature", "Gherkin" },
			{ ".frag", "GLSL" },
			{ ".fs", "Fsharp" },
			{ ".fsi", "Fsharp" },
			{ ".fsx", "Fsharp" },
			{ ".ftl", "FTL" },
			{ ".g", "GAP" },
			{ ".g4", "ANTLR4" },
			{ ".gcode", "GCode" },
			{ ".gd", "GdScript" },
			{ ".ged", "GEDCOM" },
			{ ".gitattributes", "Git" },
			{ ".gitignore", "Git" },
			{ ".gitmodules", "Git" },
			{ ".glsl", "GLSL" },
			{ ".gml", "GML" },
			{ ".gn", "GN" },
			{ ".go", "Go" },
			{ ".gp", "PariGP" },
			{ ".gql", "Graphql" },
			{ ".gradle", "Gradle" },
			{ ".graph", "Roboconf" },
			{ ".graphql", "Graphql" },
			{ ".groovy", "Groovy" },
			{ ".gsh", "Groovy" },
			{ ".gv", "Dot" },
			{ ".gvy", "Groovy" },
			{ ".gy", "Groovy" },
			{ ".h", "C" },
			{ ".haml", "Haml" },
			{ ".handlebars", "Handlebars" },
			{ ".hbs", "Handlebars" },
			{ ".hcl", "HCL" },
			{ ".hdl", "Nand2Tetris-HDL" },
			{ ".hh", "Cpp" },
			{ ".hlsl", "HLSL" },
			{ ".hoon", "Hoon" },
			{ ".hpkp", "HPKP" },
			{ ".hpp", "Cpp" },
			{ ".hrl", "Erlang" },
			{ ".hs", "Haskell" },
			{ ".hsts", "HSTS" },
			{ ".htaccess", "ApacheConf" },
			{ ".http", "HTTP" },
			{ ".htm", "html" },
			{ ".hx", "Haxe" },
			{ ".hxx", "Cpp" },
			{ ".i7x", "Inform7" },
			{ ".icn", "Icon" },
			{ ".icu", "ICU-Message-Format" },
			{ ".idr", "Idris" },
			{ ".igo", "PascalIGO" },
			{ ".ijs", "J" },
			{ ".il", "CIL" },
			{ ".ily", "LilyPond" },
			{ ".ino", "Arduino" },
			{ ".instances", "Roboconf" },
			{ ".io", "Io" },
			{ ".iol", "Jolie" },
			{ ".java", "Java" },
			{ ".javadoc", "JavaDoc" },
			{ ".jexl", "JEXL" },
			{ ".jl", "Julia" },
			{ ".jq", "JQ" },
			{ ".js", "JavaScript" },
			{ ".jsdoc", "JSDoc" },
			{ ".json5", "JSON5" },
			{ ".jsonp", "JSONP" },
			{ ".jsx", "JSX" },
			{ ".keepalived", "KeepALIVED" },
			{ ".keyman", "Keyman" },
			{ ".kql", "Kusto" },
			{ ".kt", "Kotlin" },
			{ ".kts", "Kotlin" },
			{ ".kum", "Kumir" },
			{ ".latte", "Latte" },
			{ ".lds", "Linker-Script" },
			{ ".less", "Less" },
			{ ".lhs", "Haskell" },
			{ ".lidr", "Idris" },
			{ ".liquid", "Liquid" },
			{ ".lisp", "Lisp" },
			{ ".ll", "LLVM" },
			{ ".log", "Log" },
			{ ".lol", "LOLCode" },
			{ ".ls", "LiveScript" },
			{ ".lsp", "Lisp" },
			{ ".lua", "Lua" },
			{ ".ly", "LilyPond" },
			{ ".m", "Matlab" },
			{ ".mak", "Makefile" },
			{ ".mc2", "WarpScript" },
			{ ".mf", "MetaFont" },
			{ ".miz", "Mizar" },
			{ ".ml", "OCaml" },
			{ ".mli", "OCaml" },
			{ ".mm", "ObjectiveC" },
			{ ".mmd", "Mermaid" },
			{ ".mongodb", "MongoDB" },
			{ ".monkey", "Monkey" },
			{ ".moon", "MoonScript" },
			{ ".n1ql", "N1QL" },
			{ ".n4js", "N4JS" },
			{ ".nanorc", "Naniscript" },
			{ ".nc", "GCode" },
			{ ".neon", "Neon" },
			{ ".nevod", "Nevod" },
			{ ".nginxconf", "Nginx" },
			{ ".ni", "Inform7" },
			{ ".nim", "Nim" },
			{ ".nix", "Nix" },
			{ ".nsh", "NSIS" },
			{ ".nsi", "NSIS" },
			{ ".nut", "Squirrel" },
			{ ".odin", "Odin" },
			{ ".ol", "Jolie" },
			{ ".oz", "Oz" },
			{ ".pas", "Pascal" },
			{ ".patch", "Diff" },
			{ ".pb", "PureBasic" },
			{ ".pcode", "PeopleCode" },
			{ ".pde", "Processing" },
			{ ".php", "PHP" },
			{ ".php4", "PHP" },
			{ ".php5", "PHP" },
			{ ".phpdoc", "PHPDoc" },
			{ ".phtml", "PHP" },
			{ ".pl", "Perl" },
			{ ".plb", "PLSQL" },
			{ ".pls", "PLSQL" },
			{ ".pm", "Perl" },
			{ ".po", "Gettext" },
			{ ".pot", "Gettext" },
			{ ".pp", "Pascal" },
			{ ".pq", "PowerQuery" },
			{ ".promql", "PromQL" },
			{ ".properties", "Properties" },
			{ ".proto", "Protobuf" },
			{ ".ps1", "PowerShell" },
			{ ".psl", "PSL" },
			{ ".psm1", "PowerShell" },
			{ ".pu", "Plant-UML" },
			{ ".pug", "Pug" },
			{ ".puml", "Plant-UML" },
			{ ".pure", "Pure" },
			{ ".purs", "PureScript" },
			{ ".px", "PCAxis" },
			{ ".py", "Python" },
			{ ".pyi", "Python" },
			{ ".pyw", "Python" },
			{ ".q", "Q" },
			{ ".qasm", "OpenQASM" },
			{ ".qml", "QML" },
			{ ".qs", "QSharp" },
			{ ".r", "R" },
			{ ".rb", "Ruby" },
			{ ".rbw", "Ruby" },
			{ ".re", "Reason" },
			{ ".rego", "Rego" },
			{ ".rei", "Reason" },
			{ ".res", "Rescript" },
			{ ".rest", "REST" },
			{ ".rip", "RIP" },
			{ ".rkt", "Racket" },
			{ ".robot", "RobotFramework" },
			{ ".rpy", "RenPY" },
			{ ".rq", "SPARQL" },
			{ ".rs", "Rust" },
			{ ".rscript", "R" },
			{ ".rules", "Firestore-Security-Rules" },
			{ ".s", "ARMASM" },
			{ ".sas", "SAS" },
			{ ".scala", "Scala" },
			{ ".scd", "SuperCollider" },
			{ ".scm", "Scheme" },
			{ ".scss", "SCSS" },
			{ ".service", "Systemd" },
			{ ".sh", "Bash" },
			{ ".sh-session", "Shell-Session" },
			{ ".sln", "Solution-File" },
			{ ".smali", "Smali" },
			{ ".sml", "SML" },
			{ ".socket", "Systemd" },
			{ ".sol", "Solidity" },
			{ ".soy", "Soy" },
			{ ".spl", "Splunk-SPL" },
			{ ".sqf", "SQF" },
			{ ".st", "SmallTalk" },
			{ ".stan", "Stan" },
			{ ".sty", "Latex" },
			{ ".styl", "Stylus" },
			{ ".swift", "Swift" },
			{ ".tap", "TAP" },
			{ ".tcl", "Tcl" },
			{ ".tex", "Latex" },
			{ ".textile", "Textile" },
			{ ".tf", "HCL" },
			{ ".toml", "TOML" },
			{ ".tpl", "Smarty" },
			{ ".tremor", "Tremor" },
			{ ".ts", "TypeScript" },
			{ ".tsx", "TSX" },
			{ ".tt", "T4-Templating" },
			{ ".tt2", "TT2" },
			{ ".ttl", "Turtle" },
			{ ".twig", "Twig" },
			{ ".typoscript", "TypoScript" },
			{ ".uc", "UnrealScript" },
			{ ".v", "V" },
			{ ".vala", "Vala" },
			{ ".vb", "VBNet" },
			{ ".vert", "GLSL" },
			{ ".vhdl", "VHDL" },
			{ ".vim", "Vim" },
			{ ".vm", "Velocity" },
			{ ".wat", "WASM" },
			{ ".webidl", "Web-IDL" },
			{ ".wgsl", "WGSL" },
			{ ".wiki", "Wiki" },
			{ ".wl", "Wolfram" },
			{ ".wls", "Wolfram" },
			{ ".wren", "Wren" },
			{ ".xeora", "Xeora" },
			{ ".xeoracube", "Xeora" },
			{ ".xl", "Excel-Formula" },
			{ ".xls", "Excel-Formula" },
			{ ".xlsx", "Excel-Formula" },
			{ ".xojo_code", "Xojo" },
			{ ".xojo_menu", "Xojo" },
			{ ".xq", "XQuery" },
			{ ".xquery", "XQuery" },
			{ ".y", "Bison" },
			{ ".yang", "Yang" },
			{ ".zig", "ZigetLua" },
			{ "CMakeLists.txt", "CMake" },
			{ "Dockerfile", "Docker" },
			{ "go.mod", "Go-Module" },
			{ "go.sum", "Go-Module" },
			{ "makefile", "Makefile" },
		};
	}

}
