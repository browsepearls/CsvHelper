﻿// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a <see cref="string"/> to and from a <see cref="string"/>.
	/// </summary>
	public class StringConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString(ReadOnlySpan<char> text, IReaderRow row, MemberMapData memberMapData)
		{
			foreach (var nullValue in memberMapData.TypeConverterOptions.NullValues)
			{
				if (ArrayHelper.Equals(nullValue, text))
				{
					return null;
				}
			}

			return text.ToString();
		}
	}
}
