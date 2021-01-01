using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Parses a raw field and returns a string.
	/// </summary>
	/// <param name="span">The raw field.</param>
	/// <param name="context">Information to help parse.</param>
	/// <returns>The parsed field.</returns>
	public delegate string ParseFieldFunc(Span<char> span, ParseFieldOptions context);
}
