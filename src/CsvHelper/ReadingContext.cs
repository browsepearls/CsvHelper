// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CsvHelper
{
	/// <summary>
	/// CSV reading state.
	/// </summary>
	public class ReadingContext
	{
		/// <summary>
		/// Gets the parser.
		/// </summary>
		public IParser Parser { get; private set; }

		/// <summary>
		/// Gets the reader.
		/// </summary>
		public IReader Reader { get; internal set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ReadingContext"/> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public ReadingContext(IReader reader)
		{
			Reader = reader;
			Parser = reader.Parser;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ReadingContext"/> class.
		/// </summary>
		/// <param name="parser">The parser.</param>
		public ReadingContext(IParser parser)
		{
			Parser = parser;
		}
	}
}
