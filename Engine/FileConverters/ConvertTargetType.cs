﻿using System.ComponentModel;

namespace JocysCom.VS.AiCompanion.Engine.FileConverters
{
	public enum ConvertTargetType

	{
		[Description("Convert To")]
		None = 0,
		[Description("JSON (*.json)")]
		JSON,
		[Description("JSON Lines (*.jsonl)")]
		JSONL,
		[Description("Rich Text Format (*.rtf)")]
		RTF,
		[Description("Comma Separated Values (*.csv)")]
		CSV,
		[Description("Markdown (*.md)")]
		MD,
		[Description("Microsoft Excel Worksheet (*xlsx)")]
		XLSX,
		[Description("Microsoft Word Document (*.docx)")]
		DOCX,
		[Description("Text Files (*.txt)")]
		TXTS,
	}
}
