using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Function that gets called when bad data is found.
	/// </summary>
	/// <param name="context">The context.</param>
	public delegate void BadDataFound(CsvContext context);
}
