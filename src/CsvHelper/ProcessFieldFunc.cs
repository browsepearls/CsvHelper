using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function to processes a raw field.
	/// </summary>
	/// <param name="span">The raw field.</param>
	/// <param name="options">Processing options.</param>
	/// <returns>The processed field.</returns>
	public delegate ReadOnlySpan<char> ProcessFieldFunc(ReadOnlySpan<char> span, ProcessFieldOptions options);

	/// <summary>
	/// Processing to run before dequoting a field.
	/// </summary>
	/// <param name="span">The field.</param>
	/// <param name="options">Processing options.</param>
	/// <returns>The processed field.</returns>
	public delegate ReadOnlySpan<char> PreDequoteFieldFunc(ReadOnlySpan<char> span, ProcessFieldOptions options);

	/// <summary>
	/// Processing that removes quotes.
	/// </summary>
	/// <param name="span">The field.</param>
	/// <param name="options">Processing options.</param>
	/// <returns>The processed field.</returns>
	public delegate ReadOnlySpan<char> DequoteFieldFunc(ReadOnlySpan<char> span, ProcessFieldOptions options);

	/// <summary>
	/// Processing to run after dequoting a field.
	/// </summary>
	/// <param name="span">The field.</param>
	/// <param name="options">Processing options.</param>
	/// <returns>The processed field.</returns>
	public delegate ReadOnlySpan<char> PostDequoteFieldFunc(ReadOnlySpan<char> span, ProcessFieldOptions options);
}
