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
    public readonly ref struct ParseFieldOptions
    {
		/// <summary>
		/// The character used to quote fields.
		/// </summary>
		public char Quote { get; init; }

		/// <summary>
		/// The escape character used to escape a quote inside a field.
		/// </summary>
		public char Escape { get; init; }

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
		/// Method to call when bad data is detected.
		/// </summary>
		public Action<ReadingContext> BadDataFound { get; init; }

		/// <summary>
		/// The reading context.
		/// </summary>
		public ReadingContext Context { get; init; }
	}
}
