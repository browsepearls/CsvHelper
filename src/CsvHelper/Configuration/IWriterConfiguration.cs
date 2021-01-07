// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.TypeConversion;
using System.Collections.Generic;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for the <see cref="IWriter"/>.
	/// </summary>
	public interface IWriterConfiguration : ISerializerConfiguration
	{
		/// <summary>
		/// Gets a string representation of the currently configured Quote character.
		/// </summary>
		/// <value>
		/// The new quote string.
		/// </value>
		string QuoteString { get; }

		/// <summary>
		/// Gets a string representation of two of the currently configured Quote characters.
		/// </summary>
		/// <value>
		/// The new double quote string.
		/// </value>
		string DoubleQuoteString { get; }

		/// <summary>
		/// Gets a function that is used to determine if a field should get quoted 
		/// when writing.
		/// Arguments: field, context
		/// </summary>
		Func<string, CsvContext, bool> ShouldQuote { get; }

		/// <summary>
		/// Gets the culture info used to read an write CSV files.
		/// </summary>
		CultureInfo CultureInfo { get; }

		/// <summary>
		/// Gets a value indicating if comments are allowed.
		/// True to allow commented out lines, otherwise false.
		/// </summary>
		bool AllowComments { get; }

		/// <summary>
		/// Gets the character used to denote
		/// a line that is commented out. Default is '#'.
		/// </summary>
		char Comment { get; }

		/// <summary>
		/// Gets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		bool HasHeaderRecord { get; }

		/// <summary>
		/// Gets a value indicating whether references
		/// should be ignored when auto mapping. True to ignore
		/// references, otherwise false. Default is false.
		/// </summary>
		bool IgnoreReferences { get; }

		/// <summary>
		/// Gets a value indicating if private
		/// member should be read from and written to.
		/// True to include private member, otherwise false. Default is false.
		/// </summary>
		bool IncludePrivateMembers { get; }

		/// <summary>
		/// Gets a callback that will return the prefix for a reference header.
		/// Arguments: memberType, memberName
		/// </summary>
		Func<Type, string, string> ReferenceHeaderPrefix { get; }

		/// <summary>
		/// Gets the member types that are used when auto mapping.
		/// MemberTypes are flags, so you can choose more than one.
		/// Default is Properties.
		/// </summary>
		MemberTypes MemberTypes { get; }

		/// <summary>
		/// Gets a value indicating that during writing if a new 
		/// object should be created when a reference member is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference member's member.
		/// </summary>
		bool UseNewObjectForNullReferenceMembers { get; }

		/// <summary>
		/// Gets the comparer used to order the properties
		/// of dynamic objects when writing. The default is null,
		/// which will preserve the order the object properties
		/// were created with.
		/// </summary>
		IComparer<string> DynamicPropertySort { get; }
	}
}
