// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// CSV writing state.
	/// </summary>
	public class WritingContext
	{
		/// <summary>
		/// Gets the writer.
		/// </summary>
		public IWriter Writer { get; internal set; }

		/// <summary>
		/// Gets the serializer.
		/// </summary>
		public ISerializer Serializer { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="WritingContext"/> class.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public WritingContext(IWriter writer)
		{
			Writer = writer;
			Serializer = writer.Serializer;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WritingContext"/> class.
		/// </summary>
		/// <param name="serializer">The serializer.</param>
		public WritingContext(ISerializer serializer)
		{
			Serializer = serializer;
		}
	}
}
