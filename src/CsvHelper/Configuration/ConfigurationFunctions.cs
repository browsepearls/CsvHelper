// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CsvHelper.Configuration
{
	/// <summary>Holds the default callback methods for delegate members of <c>CsvHelper.Configuration.Configuration</c>.</summary>
	public static class ConfigurationFunctions
	{
		private static readonly char[] quoteChars = new char[] { '\r', '\n' };

		/// <summary>
		/// Throws a <see cref="ValidationException"/> if <paramref name="invalidHeaders"/> is not empty.
		/// </summary>
		public static void HeaderValidated(InvalidHeader[] invalidHeaders, CsvContext context)
		{
			if (invalidHeaders.Count() == 0)
			{
				return;
			}

			var errorMessage = new StringBuilder();
			foreach (var invalidHeader in invalidHeaders)
			{
				errorMessage.AppendLine($"Header with name '{string.Join("' or '", invalidHeader.Names)}'[{invalidHeader.Index}] was not found.");
			}

			var messagePostfix =
				$"If you are expecting some headers to be missing and want to ignore this validation, " +
				$"set the configuration {nameof(HeaderValidated)} to null. You can also change the " +
				$"functionality to do something else, like logging the issue.";
			errorMessage.AppendLine(messagePostfix);

			throw new HeaderValidationException(context, invalidHeaders, errorMessage.ToString());
		}

		/// <summary>
		/// Throws a <c>MissingFieldException</c>.
		/// </summary>
		public static void MissingFieldFound(string[] headerNames, int index, CsvContext context)
		{
			var messagePostfix = $"You can ignore missing fields by setting {nameof(MissingFieldFound)} to null.";

			// Get by index.

			if (headerNames == null || headerNames.Length == 0)
			{
				throw new MissingFieldException(context, $"Field at index '{index}' does not exist. {messagePostfix}");
			}

			// Get by name.

			var indexText = index > 0 ? $" at field index '{index}'" : string.Empty;

			if (headerNames.Length == 1)
			{
				throw new MissingFieldException(context, $"Field with name '{headerNames[0]}'{indexText} does not exist. {messagePostfix}");
			}

			throw new MissingFieldException(context, $"Field containing names '{string.Join("' or '", headerNames)}'{indexText} does not exist. {messagePostfix}");
		}

		/// <summary>
		/// Throws a <see cref="BadDataException"/>.
		/// </summary>
		public static void BadDataFound(CsvContext context)
		{
			throw new BadDataException(context, $"You can ignore bad data by setting {nameof(BadDataFound)} to null.");
		}

		/// <summary>
		/// Throws the given <paramref name="exception"/>.
		/// </summary>
		public static bool ReadingExceptionOccurred(CsvHelperException exception)
		{
			return true;
		}

		/// <summary>
		/// Returns true if the field contains a <see cref="IWriterConfiguration.QuoteString"/>,
		/// starts with a space, ends with a space, contains \r or \n, or contains
		/// the <see cref="ISerializerConfiguration.Delimiter"/>.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <param name="context">The context.</param>
		/// <returns></returns>
		public static bool ShouldQuote(string field, CsvContext context)
		{
			var shouldQuote = !string.IsNullOrEmpty(field) && 
			(
				field.Contains(context.Writer.Configuration.QuoteString) // Contains quote
				|| field[0] == ' ' // Starts with a space
				|| field[field.Length - 1] == ' ' // Ends with a space
				|| field.IndexOfAny(quoteChars) > -1 // Contains chars that require quotes
				|| (context.Writer.Configuration.Delimiter.Length > 0 && field.Contains(context.Writer.Configuration.Delimiter)) // Contains delimiter
			);

			return shouldQuote;
		}

		/// <summary>
		/// Returns <c>false</c>.
		/// </summary>
		public static bool ShouldSkipRecord(string[] record)
		{
			return false;
		}

		/// <summary>
		/// Returns the <paramref name="header"/> as given.
		/// </summary>
		public static string PrepareHeaderForMatch(string header, int index)
		{
			return header;
		}

		/// <summary>
		/// Returns <c>true</c> if <paramref name="type"/>:
		/// 1. does not have a parameterless constructor
		/// 2. has a constructor
		/// 3. is not a user defined struct
		/// 4. is not an interface
		/// 5. TypeCode is not an Object.
		/// </summary>
		public static bool ShouldUseConstructorParameters(Type type)
		{
			return !type.HasParameterlessConstructor()
				&& type.HasConstructor()
				&& !type.IsUserDefinedStruct()
				&& !type.IsInterface
				&& Type.GetTypeCode(type) == TypeCode.Object;
		}

		/// <summary>
		/// Returns the type's constructor with the most parameters. 
		/// If two constructors have the same number of parameters, then
		/// there is no guarantee which one will be returned. If you have
		/// that situation, you should probably implement this function yourself.
		/// </summary>
		public static ConstructorInfo GetConstructor(Type type)
		{
			return type.GetConstructorWithMostParameters();
		}

		/// <summary>
		/// Returns the header name ran through <see cref="PrepareHeaderForMatch(string, int)"/>.
		/// If no header exists, property names will be Field1, Field2, Field3, etc.
		/// </summary>
		/// <param name="context">The <see cref="ReadingContext"/>.</param>
		/// <param name="fieldIndex">The field index of the header to get the name for.</param>
		public static string GetDynamicPropertyName(CsvContext context, int fieldIndex)
		{
			if (context.Reader.HeaderRecord == null)
			{
				return $"Field{fieldIndex + 1}";
			}

			var header = context.Reader.HeaderRecord[fieldIndex];
			header = context.Reader.Configuration.PrepareHeaderForMatch(header, fieldIndex);

			return header;
		}

		/// <summary>
		/// Calls the field parsing pipeline and returns the output.
		/// PreDequote -> Dequote -> PostDequote
		/// </summary>
		/// <returns>The processed field.</returns>
		public static Span<char> ProcessField(Span<char> span, ProcessFieldOptions options)
		{
			span = options.PreDequote(span, options);
			span = options.Dequote(span, options);
			span = options.PostDequote(span, options);

			return span;
		}

		/// <summary>
		/// Trims the field if enabled.
		/// </summary>
		/// <returns>The processed field.</returns>
		public static Span<char> PreDequoteField(Span<char> span, ProcessFieldOptions options)
		{
			if ((options.TrimOptions & TrimOptions.Trim) != TrimOptions.Trim)
			{
				return span;
			}

			if ((options.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
			{
				span = ArrayHelper.Trim(span, options.WhiteSpaceChars);
			}

			return span;
		}

		/// <summary>
		/// Removes quoting and escape chars from a field.
		/// </summary>
		/// <returns>The processed field.</returns>
		public static Span<char> DequoteField(Span<char> span, ProcessFieldOptions options)
		{
			if (!options.IsQuoted || options.IgnoreQuotes)
			{
				return span;
			}

			// Remove the quotes from the ends.
			if (span[0] == options.Quote && span[span.Length - 1] == options.Quote && span.Length > 1)
			{
				span = span.Slice(1, span.Length - 2);
			}
			else
			{
				options.BadDataFound?.Invoke(options.Context);

				// If BadDataFound doesn't throw, we don't want to remove the esacpe characters.
				// Field isn't quoted properly, so leave it as is.

				return span;
			}

			// Remove the escape characters.
			var totalLength = 0;
			var length = 0;
			var inEscape = false;
			var segmentStart = 0;
			var segments = new List<Segment>();
			for (var i = 0; i < span.Length; i++)
			{
				var c = span[i];

				if (options.LineBreakInQuotedFieldIsBadData && (c == '\r' || c == '\n'))
				{
					options.BadDataFound?.Invoke(options.Context);
				}

				if (inEscape)
				{
					inEscape = false;
					segmentStart = i;

					if (c != options.Quote)
					{
						options.BadDataFound?.Invoke(options.Context);
					}

					continue;
				}

				if (c == options.Escape)
				{
					inEscape = true;
					length = i - segmentStart;
					totalLength += length;

					if (length > 0)
					{
						segments.Add(new Segment
						{
							Start = segmentStart,
							Length = length,
						});
					}
				}
			}

			if (inEscape)
			{
				options.BadDataFound?.Invoke(options.Context);
			}

			if (segmentStart == 0)
			{
				// No escapes were found.
				return span;
			}

			// Escapes were found. The span needs to be split up
			// and reassembled into a new span.

			length = span.Length - segmentStart;
			totalLength += length;

			segments.Add(new Segment
			{
				Start = segmentStart,
				Length = length,
			});

			var combined = new Span<char>(new char[totalLength]);

			var combinedPosition = 0;
			for (var i = 0; i < segments.Count; i++)
			{
				span.Slice(segments[i].Start, segments[i].Length).CopyTo(combined.Slice(combinedPosition));
				combinedPosition += segments[i].Length;
			}

			return combined;
		}

		/// <summary>
		/// Trims inside the quotes if enabled.
		/// </summary>
		public static Span<char> PostDequoteField(Span<char> span, ProcessFieldOptions options)
		{
			if (!options.IsQuoted)
			{
				return span;
			}

			if ((options.TrimOptions & TrimOptions.InsideQuotes) == TrimOptions.InsideQuotes)
			{
				span = ArrayHelper.Trim(span, options.WhiteSpaceChars);
			}

			return span;
		}

		[DebuggerDisplay("Start = {Start}, Length = {Length}")]
		private readonly struct Segment
		{
			public int Start { get; init; }

			public int Length { get; init; }
		}
	}
}
