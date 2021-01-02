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
	public delegate Span<char> ProcessFieldFunc(Span<char> span, ProcessFieldOptions options);

	/// <summary>
	/// Processing to run before dequoting a field.
	/// </summary>
	/// <param name="span">The field.</param>
	/// <param name="options">Processing options.</param>
	/// <returns>The processed field.</returns>
	public delegate Span<char> PreDequoteFieldFunc(Span<char> span, ProcessFieldOptions options);

	/// <summary>
	/// Processing that removes quotes.
	/// </summary>
	/// <param name="span">The field.</param>
	/// <param name="options">Processing options.</param>
	/// <returns>The processed field.</returns>
	public delegate Span<char> DequoteFieldFunc(Span<char> span, ProcessFieldOptions options);

	/// <summary>
	/// Processing to run after dequoting a field.
	/// </summary>
	/// <param name="span">The field.</param>
	/// <param name="options">Processing options.</param>
	/// <returns>The processed field.</returns>
	public delegate Span<char> PostDequoteFieldFunc(Span<char> span, ProcessFieldOptions options);
}
