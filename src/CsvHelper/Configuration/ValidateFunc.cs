using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Function to validate a field.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <returns><c>true</c> if valid, otherwise <c>false</c>.</returns>
	public delegate bool ValidateFunc(ReadOnlySpan<char> field);
}
