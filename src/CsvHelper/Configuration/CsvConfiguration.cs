// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for reading and writing CSV data.
	/// </summary>
	public record CsvConfiguration : IReaderConfiguration, IWriterConfiguration
	{
		private string delimiter = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
		private char escape = '"';
		private char quote = '"';
		private string quoteString = "\"";
		private string doubleQuoteString = "\"\"";
		private NewLine newLine;

		/// <summary>
		/// Gets a value indicating whether to leave the <see cref="TextReader"/> open after the <see cref="CsvReader"/> object is disposed.
		/// </summary>
		/// <value>
		///   <c>true</c> to leave the <see cref="TextReader"/> open after the <see cref="CsvReader"/> object is disposed, otherwise <c>false</c>.
		/// </value>
		public virtual bool LeaveOpen { get; init; }

		/// <summary>
		/// Gets or sets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		public virtual bool HasHeaderRecord { get; init; } = true;

		/// <summary>
		/// Gets or sets the function that is called when a header validation check is ran. The default function
		/// will throw a <see cref="ValidationException"/> if there is no header for a given member mapping.
		/// You can supply your own function to do other things like logging the issue instead of throwing an exception.
		/// Arguments: (isValid, headerNames, headerNameIndex, context)
		/// </summary>
		public virtual Action<InvalidHeader[], CsvContext> HeaderValidated { get; init; } = ConfigurationFunctions.HeaderValidated;

		/// <summary>
		/// Gets or sets the function that is called when a missing field is found. The default function will
		/// throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things
		/// like logging the issue instead of throwing an exception.
		/// Arguments: (headerNames, index, context)
		/// </summary>
		public virtual Action<string[], int, CsvContext> MissingFieldFound { get; init; } = ConfigurationFunctions.MissingFieldFound;

		/// <summary>
		/// Gets or sets the function that is called when bad field data is found. A field
		/// has bad data if it contains a quote and the field is not quoted (escaped).
		/// You can supply your own function to do other things like logging the issue
		/// instead of throwing an exception.
		/// Arguments: context
		/// </summary>
		public virtual Action<CsvContext> BadDataFound { get; init; } = ConfigurationFunctions.BadDataFound;

		/// <summary>
		/// Gets or sets the function that is called when a reading exception occurs.
		/// The default function will re-throw the given exception. If you want to ignore
		/// reading exceptions, you can supply your own function to do other things like
		/// logging the issue.
		/// Arguments: (exception)
		/// </summary>
		public virtual Func<CsvHelperException, bool> ReadingExceptionOccurred { get; init; } = ConfigurationFunctions.ReadingExceptionOccurred;

		/// <summary>
		/// Gets or sets the callback that will be called to
		/// determine whether to skip the given record or not.
		/// Arguments: (record)
		/// </summary>
		public virtual Func<string[], bool> ShouldSkipRecord { get; init; } = ConfigurationFunctions.ShouldSkipRecord;

		/// <summary>
		/// Gets or sets a value indicating if a line break found in a quote field should
		/// be considered bad data. True to consider a line break bad data, otherwise false.
		/// Defaults to false.
		/// </summary>
		public virtual bool LineBreakInQuotedFieldIsBadData { get; init; }

		/// <summary>
		/// Gets or sets a value indicating if fields should be sanitized
		/// to prevent malicious injection. This covers MS Excel, 
		/// Google Sheets and Open Office Calc.
		/// </summary>
		public virtual bool SanitizeForInjection { get; init; }

		/// <summary>
		/// Gets or sets the characters that are used for injection attacks.
		/// </summary>
		public virtual char[] InjectionCharacters { get; init; } = new[] { '=', '@', '+', '-' };

		/// <summary>
		/// Gets or sets the character used to escape a detected injection.
		/// </summary>
		public virtual char InjectionEscapeCharacter { get; init; } = '\t';

		/// <summary>
		/// Gets or sets a value indicating whether changes in the column
		/// count should be detected. If true, a <see cref="BadDataException"/>
		/// will be thrown if a different column count is detected.
		/// </summary>
		/// <value>
		/// <c>true</c> if [detect column count changes]; otherwise, <c>false</c>.
		/// </value>
		public virtual bool DetectColumnCountChanges { get; init; }

		/// <summary>
		/// Prepares the header field for matching against a member name.
		/// The header field and the member name are both ran through this function.
		/// You should do things like trimming, removing whitespace, removing underscores,
		/// and making casing changes to ignore case.
		/// Arguments: (header, fieldIndex)
		/// </summary>
		public virtual Func<string, int, string> PrepareHeaderForMatch { get; init; } = ConfigurationFunctions.PrepareHeaderForMatch;

		/// <summary>
		/// Determines if constructor parameters should be used to create
		/// the class instead of the default constructor and members.
		/// Arguments: (parameterType)
		/// </summary>
		public virtual Func<Type, bool> ShouldUseConstructorParameters { get; init; } = ConfigurationFunctions.ShouldUseConstructorParameters;

		/// <summary>
		/// Chooses the constructor to use for constructor mapping.
		/// Arguments: (classType)
		/// </summary>
		public virtual Func<Type, ConstructorInfo> GetConstructor { get; init; } = ConfigurationFunctions.GetConstructor;

		/// <summary>
		/// Gets or sets the comparer used to order the properties
		/// of dynamic objects when writing. The default is null,
		/// which will preserve the order the object properties
		/// were created with.
		/// </summary>
		public virtual IComparer<string> DynamicPropertySort { get; init; }

		/// <summary>
		/// Gets the name to use for the property of the dynamic object.
		/// Arguments: (readingContext, fieldIndex)
		/// </summary>
		public virtual Func<CsvContext, int, string> GetDynamicPropertyName { get; init; } = ConfigurationFunctions.GetDynamicPropertyName;

		/// <summary>
		/// Processes a raw field.
		/// This method calls the field parsing pipeline.
		/// PreDequote -> Dequote -> PostDequote
		/// </summary>
		public virtual ProcessFieldFunc ProcessField { get; init; } = ConfigurationFunctions.ProcessField;

		/// <summary>
		/// Processing that happens to a field before dequoting.
		/// </summary>
		public virtual PreDequoteFieldFunc PreDequoteField { get; init; } = ConfigurationFunctions.PreDequoteField;

		/// <summary>
		/// Removes quoting from a field.
		/// </summary>
		public virtual DequoteFieldFunc DequoteField { get; init; } = ConfigurationFunctions.DequoteField;

		/// <summary>
		/// Processing that happens to a field after dequoting.
		/// </summary>
		public virtual PostDequoteFieldFunc PostDequoteField { get; init; } = ConfigurationFunctions.PostDequoteField;

		/// <summary>
		/// Gets or sets a value indicating whether references
		/// should be ignored when auto mapping. True to ignore
		/// references, otherwise false. Default is false.
		/// </summary>
		public virtual bool IgnoreReferences { get; init; }

		/// <summary>
		/// Gets or sets the field trimming options.
		/// </summary>
		public virtual TrimOptions TrimOptions { get; init; }

		/// <summary>
		/// Characters considered whitespace.
		/// Used when trimming fields.
		/// </summary>
		public virtual char[] WhiteSpaceChars { get; init; } = new char[] { ' ', '\t' };

		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// Default is CultureInfo.TextInfo.ListSeparator.
		/// </summary>
		public virtual string Delimiter
		{
			get { return delimiter; }
			init
			{
				if (value == "\n")
				{
					throw new ConfigurationException("Newline is not a valid delimiter.");
				}

				if (value == "\r")
				{
					throw new ConfigurationException("Carriage return is not a valid delimiter.");
				}

				if (value == Convert.ToString(quote))
				{
					throw new ConfigurationException("You can not use the quote as a delimiter.");
				}

				delimiter = value;
			}
		}

		/// <summary>
		/// Gets or sets the escape character used to escape a quote inside a field.
		/// Default is '"'.
		/// </summary>
		public virtual char Escape
		{
			get { return escape; }
			init
			{
				if (value == '\n')
				{
					throw new ConfigurationException("Newline is not a valid escape.");
				}

				if (value == '\r')
				{
					throw new ConfigurationException("Carriage return is not a valid escape.");
				}

				if (value.ToString() == delimiter)
				{
					throw new ConfigurationException("You can not use the delimiter as an escape.");
				}

				escape = value;

				doubleQuoteString = escape + quoteString;
			}
		}

		/// <summary>
		/// Gets or sets the character used to quote fields.
		/// Default is '"'.
		/// </summary>
		public virtual char Quote
		{
			get { return quote; }
			init
			{
				if (value == '\n')
				{
					throw new ConfigurationException("Newline is not a valid quote.");
				}

				if (value == '\r')
				{
					throw new ConfigurationException("Carriage return is not a valid quote.");
				}

				if (value == '\0')
				{
					throw new ConfigurationException("Null is not a valid quote.");
				}

				if (Convert.ToString(value) == delimiter)
				{
					throw new ConfigurationException("You can not use the delimiter as a quote.");
				}

				quote = value;

				quoteString = Convert.ToString(value, CultureInfo);
				doubleQuoteString = escape + quoteString;
			}
		}

		/// <summary>
		/// Gets a string representation of the currently configured Quote character.
		/// </summary>
		/// <value>
		/// The new quote string.
		/// </value>
		public virtual string QuoteString => quoteString;

		/// <summary>
		/// Gets a string representation of two of the currently configured Quote characters.
		/// </summary>
		/// <value>
		/// The new double quote string.
		/// </value>
		public virtual string DoubleQuoteString => doubleQuoteString;

		/// <summary>
		/// Gets or sets a function that is used to determine if a field should get quoted
		/// when writing.
		/// Arguments: field, context
		/// </summary>
		public Func<string, CsvContext, bool> ShouldQuote { get; init; } = ConfigurationFunctions.ShouldQuote;

		/// <summary>
		/// Gets or sets the character used to denote
		/// a line that is commented out. Default is '#'.
		/// </summary>
		public virtual char Comment { get; init; } = '#';

		/// <summary>
		/// Gets or sets a value indicating if comments are allowed.
		/// True to allow commented out lines, otherwise false.
		/// </summary>
		public virtual bool AllowComments { get; init; }

		/// <summary>
		/// Gets or sets the size of the buffer
		/// used for reading CSV files.
		/// Default is 2048.
		/// </summary>
		public virtual int BufferSize { get; init; } = 2048;

		/// <summary>
		/// Gets or sets a value indicating whether the number of bytes should
		/// be counted while parsing. Default is false. This will slow down parsing
		/// because it needs to get the byte count of every char for the given encoding.
		/// The <see cref="Encoding"/> needs to be set correctly for this to be accurate.
		/// </summary>
		public virtual bool CountBytes { get; init; }

		/// <summary>
		/// Gets or sets the encoding used when counting bytes.
		/// </summary>
		public virtual Encoding Encoding { get; init; } = Encoding.UTF8;

		/// <summary>
		/// Gets or sets the culture info used to read an write CSV files.
		/// Default is <see cref="System.Globalization.CultureInfo.CurrentCulture"/>.
		/// </summary>
		public virtual CultureInfo CultureInfo { get; protected set; }

		/// <summary>
		/// Gets or sets a value indicating if quotes should be
		/// ignored when parsing and treated like any other character.
		/// </summary>
		public virtual bool IgnoreQuotes { get; init; }

		/// <summary>
		/// Gets or sets a value indicating if private
		/// member should be read from and written to.
		/// True to include private member, otherwise false. Default is false.
		/// </summary>
		public virtual bool IncludePrivateMembers { get; init; }

		/// <summary>
		/// Gets or sets the member types that are used when auto mapping.
		/// MemberTypes are flags, so you can choose more than one.
		/// Default is Properties.
		/// </summary>
		public virtual MemberTypes MemberTypes { get; init; } = MemberTypes.Properties;

		/// <summary>
		/// Gets or sets a value indicating if blank lines
		/// should be ignored when reading.
		/// True to ignore, otherwise false. Default is true.
		/// </summary>
		public virtual bool IgnoreBlankLines { get; init; } = true;

		/// <summary>
		/// Gets or sets a callback that will return the prefix for a reference header.
		/// Arguments: (memberType, memberName)
		/// </summary>
		public virtual Func<Type, string, string> ReferenceHeaderPrefix { get; init; }

		/// <summary>
		/// Gets or sets the newline to use when writing.
		/// </summary>
		public virtual NewLine NewLine
		{
			get => newLine;
			init
			{
				newLine = value;

				switch (value)
				{
					case NewLine.CR:
						NewLineString = NewLines.CR;
						break;
					case NewLine.LF:
						NewLineString = NewLines.LF;
						break;
					case NewLine.Environment:
						NewLineString = Environment.NewLine;
						break;
					default:
						NewLineString = NewLines.CRLF;
						break;
				}
			}
		}

		/// <summary>
		/// Gets the newline string to use when writing. This string is determined
		/// by the <see cref="NewLine"/> value.
		/// </summary>
		public virtual string NewLineString { get; protected set; }

		/// <summary>
		/// Gets or sets a value indicating that during writing if a new 
		/// object should be created when a reference member is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference member's member.
		/// </summary>
		public virtual bool UseNewObjectForNullReferenceMembers { get; init; } = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvConfiguration"/> class
		/// using the given <see cref="System.Globalization.CultureInfo"/>. Since <see cref="Delimiter"/>
		/// uses <see cref="CultureInfo"/> for it's default, the given <see cref="System.Globalization.CultureInfo"/>
		/// will be used instead.
		/// </summary>
		/// <param name="cultureInfo">The culture information.</param>
		public CsvConfiguration(CultureInfo cultureInfo)
		{
			CultureInfo = cultureInfo;
			delimiter = cultureInfo.TextInfo.ListSeparator;
			NewLine = cultureInfo == CultureInfo.InvariantCulture ? NewLine.CRLF : NewLine.Environment;
		}
	}
}
