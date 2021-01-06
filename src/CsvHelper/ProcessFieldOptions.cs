using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Information to help parse a field.
	/// </summary>
    public readonly ref struct ProcessFieldOptions
    {
		/// <summary>
		/// Method to call when bad data is detected.
		/// </summary>
		public Action<ReadingContext> BadDataFound { get; init; }

		/// <summary>
		/// The reading context.
		/// </summary>
		public ReadingContext Context { get; init; }

		/// <summary>
		/// The escape character used to escape a quote inside a field.
		/// </summary>
		public char Escape { get; init; }

		/// <summary>
		/// A value indicating if the field is quoted.
		/// </summary>
		/// <value>
		///   <c>true</c> if this instance is quoted; otherwise, <c>false</c>.
		/// </value>
		public bool IsQuoted { get; init; }

		/// <summary>
		/// A value indicating if quotes should be ignored when
		/// parsing and treated like any other character.
		/// </summary>
		public bool IgnoreQuotes { get; init; }

		/// <summary>
		/// A value indicating if a line break found in a quote field should
		/// be considered bad data.
		/// </summary>
		public bool LineBreakInQuotedFieldIsBadData { get; init; }

		/// <summary>
		/// The character used to quote fields.
		/// </summary>
		public char Quote { get; init; }

		/// <summary>
		/// The field trimming options.
		/// </summary>
		public TrimOptions TrimOptions { get; init; }

		/// <summary>
		/// Characters considered whitespace.
		/// Used when trimming fields.
		/// </summary>
		public char[] WhiteSpaceChars { get; init; }

		/// <summary>
		/// Function to process a field before dequoting.
		/// </summary>
		public PreDequoteFieldFunc PreDequote { get; init; }

		/// <summary>
		/// Function to remove quoting from a field.
		/// </summary>
		public DequoteFieldFunc Dequote { get; init; }

		/// <summary>
		/// Function to process a field after dequoting.
		/// </summary>
		public PostDequoteFieldFunc PostDequote { get; init; }
	}
}
