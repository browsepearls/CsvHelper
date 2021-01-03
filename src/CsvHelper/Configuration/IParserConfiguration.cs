﻿// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Text;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for the <see cref="IParser"/>.
	/// </summary>
	public interface IParserConfiguration
	{
		/// <summary>
		/// Gets the size of the buffer
		/// used for reading CSV files.
		/// Default is 2048.
		/// </summary>
		int BufferSize { get; }

		/// <summary>
		/// Gets a value indicating whether the number of bytes should
		/// be counted while parsing. Default is false. This will slow down parsing
		/// because it needs to get the byte count of every char for the given encoding.
		/// The <see cref="Encoding"/> needs to be set correctly for this to be accurate.
		/// </summary>
		bool CountBytes { get; }

		/// <summary>
		/// Gets the encoding used when counting bytes.
		/// </summary>
		Encoding Encoding { get; }

		/// <summary>
		/// Gets the function that is called when bad field data is found. A field
		/// has bad data if it contains a quote and the field is not quoted (escaped).
		/// You can supply your own function to do other things like logging the issue
		/// instead of throwing an exception.
		/// Arguments: context
		/// </summary>
		Action<ReadingContext> BadDataFound { get; }

		/// <summary>
		/// Gets a value indicating if a line break found in a quote field should
		/// be considered bad data. True to consider a line break bad data, otherwise false.
		/// Defaults to false.
		/// </summary>
		bool LineBreakInQuotedFieldIsBadData { get; }

		/// <summary>
		/// Gets the character used to denote
		/// a line that is commented out. Default is '#'.
		/// </summary>
		char Comment { get; }

		/// <summary>
		/// Gets a value indicating if comments are allowed.
		/// True to allow commented out lines, otherwise false.
		/// </summary>
		bool AllowComments { get; }

		/// <summary>
		/// Gets a value indicating if blank lines
		/// should be ignored when reading.
		/// True to ignore, otherwise false. Default is true.
		/// </summary>
		bool IgnoreBlankLines { get; }

		/// <summary>
		/// Gets a value indicating if quotes should be
		/// ignored when parsing and treated like any other character.
		/// </summary>
		bool IgnoreQuotes { get; }

		/// <summary>
		/// Gets the character used to quote fields.
		/// Default is '"'.
		/// </summary>
		char Quote { get; }

		/// <summary>
		/// Gets the delimiter used to separate fields.
		/// Default is CultureInfo.CurrentCulture.TextInfo.ListSeparator.
		/// </summary>
		string Delimiter { get; }

		/// <summary>
		/// Gets the escape character used to escape a quote inside a field.
		/// Default is '"'.
		/// </summary>
		char Escape { get; }

		/// <summary>
		/// Gets the field trimming options.
		/// </summary>
		TrimOptions TrimOptions { get; }

		/// <summary>
		/// Processes a raw field.
		/// This function calls the field parsing pipeline.
		/// PreDequote -> Dequote -> PostDequote
		/// </summary>
		ProcessFieldFunc ProcessField { get; }

		/// <summary>
		/// Processing that happens to a field before dequoting.
		/// </summary>
		PreDequoteFieldFunc PreDequoteField { get; }

		/// <summary>
		/// Removes quoting from a field.
		/// </summary>
		DequoteFieldFunc DequoteField { get; }

		/// <summary>
		/// Processing that happens to a field after dequoting.
		/// </summary>
		PostDequoteFieldFunc PostDequoteField { get; }

		/// <summary>
		/// Characters considered whitespace.
		/// Used when trimming fields.
		/// </summary>
		char[] WhiteSpaceChars { get; }
	}
}
