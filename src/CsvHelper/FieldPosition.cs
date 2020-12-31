using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// The position of a part of a field.
	/// </summary>
	[DebuggerDisplay("Start = {Start}, Length = {Length}")]
	public class FieldPosition
	{
		/// <summary>
		/// Gets or sets the start of the field.
		/// </summary>
		public int Start { get; set; }

		/// <summary>
		/// Gets or sets the length of the field.
		/// </summary>
		public int Length { get; set; }

		/// <summary>
		/// Sets <see cref="Start"/> and <see cref="Length"/> to 0.
		/// </summary>
		public void Clear()
		{
			Start = 0;
			Length = 0;
		}
	}
}
