﻿using CsvHelper.Configuration;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Parses a CSV file.
	/// </summary>
	public class CsvParser : IParser, IDisposable
	{
		private readonly TextReader reader;
		private readonly string delimiter;
		private readonly char delimiterFirstChar;
		private readonly char quote;
		private readonly char escape;
		private readonly bool countBytes;
		private readonly Encoding encoding;
		private readonly bool ignoreBlankLines;
		private readonly char comment;
		private readonly bool allowComments;
		private readonly BadDataFound badDataFound;
		private readonly bool ignoreQuotes;
		private readonly bool lineBreakInQuotedFieldIsBadData;
		private readonly TrimOptions trimOptions;
		private readonly char[] whiteSpaceChars;

		private char[] buffer;
		private int bufferSize;
		private int charsRead;
		private int bufferPosition;
		private int rowStartPosition;
		private int fieldStartPosition;
		private int row;
		private int rawRow;
		private long charCount;
		private long byteCount;
		private bool inQuotes;
		private Field[] fields;
		private int fieldsPosition;
		private bool disposed;
		private int quoteCount;
		private char[] processFieldBuffer;
		private int processFieldBufferSize;
		private ParserState state;
		private int delimiterPosition = 1;

		/// <inheritdoc/>
		public long CharCount => charCount;

		/// <inheritdoc/>
		public long ByteCount => byteCount;

		/// <inheritdoc/>
		public int Row => row;

		/// <inheritdoc/>
		public string[] Record
		{
			get
			{
				if (fieldsPosition == 0)
				{
					return null;
				}

				var record = new string[fieldsPosition];

				for (var i = 0; i < record.Length; i++)
				{
					record[i] = this[i];
				}

				return record;
			}
		}

		/// <inheritdoc/>
		public string RawRecord => new string(buffer, rowStartPosition, bufferPosition - rowStartPosition);

		/// <inheritdoc/>
		public int Count => fieldsPosition;

		/// <inheritdoc/>
		public int RawRow => rawRow;

		/// <inheritdoc/>
		public CsvContext Context { get; private set; }

		/// <inheritdoc/>
		public IParserConfiguration Configuration { get; private set; }

		/// <inheritdoc/>
		public string this[int index]
		{
			get
			{
				var field = fields[index];

				var processedField = ProcessField(field.Start + rowStartPosition, field.Length, field.QuoteCount);

				var value = new string(processedField.Buffer, processedField.Start, processedField.Length);

				return value;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvParser"/> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen">if set to <c>true</c> [leave open].</param>
		public CsvParser(TextReader reader, CultureInfo culture, bool leaveOpen = false) : this(reader, new CsvConfiguration(culture) { LeaveOpen = leaveOpen }) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvParser"/> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvParser(TextReader reader, CsvConfiguration configuration)
		{
			this.reader = reader;
			Configuration = configuration;
			Context = new CsvContext(this);

			allowComments = configuration.AllowComments;
			badDataFound = configuration.BadDataFound;
			bufferSize = configuration.BufferSize;
			comment = configuration.Comment;
			countBytes = configuration.CountBytes;
			delimiter = configuration.Delimiter;
			delimiterFirstChar = configuration.Delimiter[0];
			encoding = configuration.Encoding;
			escape = configuration.Escape;
			ignoreBlankLines = configuration.IgnoreBlankLines;
			ignoreQuotes = configuration.IgnoreQuotes;
			lineBreakInQuotedFieldIsBadData = configuration.LineBreakInQuotedFieldIsBadData;
			processFieldBufferSize = 1024;
			quote = configuration.Quote;
			whiteSpaceChars = configuration.WhiteSpaceChars;
			trimOptions = configuration.TrimOptions;

			buffer = ArrayPool<char>.Shared.Rent(bufferSize);
			processFieldBuffer = ArrayPool<char>.Shared.Rent(processFieldBufferSize);
			fields = new Field[128];
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected ProcessedField ProcessField(in int start, in int length, in int quoteCount)
		{
			var newStart = start;
			var newLength = length;

			if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
			{
				ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
			}

			if (quoteCount == 0)
			{
				// Not quoted.
				// No processing needed.

				return new ProcessedField
				{
					Buffer = buffer,
					Start = newStart,
					Length = newLength,
				};
			}

			// Remove the quotes from the ends.
			if (buffer[newStart] == quote && buffer[newStart + newLength - 1] == quote && newLength > 1)
			{
				newStart += 1;
				newLength -= 2;
			}
			else
			{
				badDataFound?.Invoke(Context);

				// If BadDataFound doesn't throw, we don't want to remove the esacpe characters.
				// Field isn't quoted properly, so leave it as is.
				// No more processing needed.

				return new ProcessedField
				{
					Buffer = buffer,
					Start = newStart,
					Length = newLength,
				};
			}

			if ((trimOptions & TrimOptions.InsideQuotes) == TrimOptions.InsideQuotes)
			{
				ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
			}

			if (lineBreakInQuotedFieldIsBadData)
			{
				for (var i = newStart; i < newStart + newLength; i++)
				{
					if (buffer[i] == '\r' || buffer[i] == '\n')
					{
						badDataFound?.Invoke(Context);
					}
				}
			}

			if (quoteCount == 2)
			{
				// The only quotes are the ends of the field.
				// No more processing is needed.
				return new ProcessedField
				{
					Buffer = buffer,
					Start = newStart,
					Length = newLength,
				};
			}

			if (newLength > processFieldBuffer.Length)
			{
				processFieldBufferSize *= 2;
				ArrayPool<char>.Shared.Return(processFieldBuffer);
				processFieldBuffer = ArrayPool<char>.Shared.Rent(processFieldBufferSize);
			}

			// Remove escapes.
			var inEscape = false;
			var position = 0;
			for (var i = newStart; i < newStart + newLength; i++)
			{
				var c = buffer[i];

				if (inEscape)
				{
					inEscape = false;
				}
				else if (c == escape)
				{
					inEscape = true;
					continue;
				}

				processFieldBuffer[position] = c;
				position++;
			}

			return new ProcessedField
			{
				Buffer = processFieldBuffer,
				Start = 0,
				Length = position,
			};
		}

		/// <inheritdoc/>
		public bool Read()
		{
			rowStartPosition = bufferPosition;
			fieldStartPosition = rowStartPosition;
			fieldsPosition = 0;
			quoteCount = 0;
			row++;
			rawRow++;

			var result = ReadLineResult.None;

			while (true)
			{
				if (bufferPosition >= charsRead)
				{
					if (!FillBuffer())
					{
						return ReadEndOfFile(result);
					}
				}

				result = ReadLine();

				if (result == ReadLineResult.Complete)
				{
					return true;
				}
			}
		}

		/// <inheritdoc/>
		public async Task<bool> ReadAsync()
		{
			rowStartPosition = bufferPosition;
			fieldStartPosition = rowStartPosition;
			fieldsPosition = 0;
			quoteCount = 0;
			row++;
			rawRow++;

			var result = ReadLineResult.None;

			while (true)
			{
				if (bufferPosition >= charsRead)
				{
					if (!await FillBufferAsync())
					{
						return ReadEndOfFile(result);
					}
				}

				result = ReadLine();

				if (result == ReadLineResult.Complete)
				{
					return true;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadLineResult ReadLine()
		{
			char c = '\0';
			char cPrev;
			while (bufferPosition < charsRead)
			{
				if (state != ParserState.None)
				{
					// Continue the state before doing anything else.
					ReadLineResult result;
					switch (state)
					{
						case ParserState.BlankLine:
							result = ReadBlankLine(ref c);
							break;
						case ParserState.Delimiter:
							result = ReadDelimiter(ref c);
							break;
						case ParserState.LineEnding:
							result = ReadLineEnding(ref c);
							break;
						default:
							result = ReadLineResult.None;
							break;
					}

					if (result == ReadLineResult.Incomplete)
					{
						return result;
					}
				}

				cPrev = c;
				c = buffer[bufferPosition];
				bufferPosition++;
				charCount++;
				if (countBytes)
				{
					byteCount += encoding.GetByteCount(new char[] { c });
				}

				if (allowComments && c == comment || ignoreBlankLines && rowStartPosition == bufferPosition - 1 && (c == '\r' || c == '\n'))
				{
					state = ParserState.BlankLine;
					var result = ReadBlankLine(ref c);
					if (result == ReadLineResult.Complete)
					{
						state = ParserState.None;

						continue;
					}
					else
					{
						return ReadLineResult.Incomplete;
					}
				}

				if (c == quote && !ignoreQuotes)
				{
					quoteCount++;
					inQuotes = !inQuotes;
				}

				if (inQuotes)
				{
					if (c == '\r' || c == '\n' && cPrev != '\r')
					{
						rawRow++;
					}

					// We don't care about anything else if we're in quotes.
					continue;
				}

				if (c == delimiterFirstChar)
				{
					state = ParserState.Delimiter;
					var result = ReadDelimiter(ref c);
					if (result == ReadLineResult.Incomplete)
					{
						return result;
					}

					state = ParserState.None;

					continue;
				}

				if (c == '\r' || c == '\n')
				{
					state = ParserState.LineEnding;
					var result = ReadLineEnding(ref c);
					if (result == ReadLineResult.Complete)
					{
						state = ParserState.None;
					}

					return result;
				}
			}

			return ReadLineResult.Incomplete;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadLineResult ReadBlankLine(ref char c)
		{
			while (bufferPosition < charsRead)
			{
				if (c == '\r' || c == '\n')
				{
					var result = ReadLineEnding(ref c);
					if (result == ReadLineResult.Complete)
					{
						rowStartPosition = bufferPosition;
						fieldStartPosition = rowStartPosition;
						row++;
						rawRow++;
					}

					return result;
				}

				c = buffer[bufferPosition];
				bufferPosition++;
				charCount++;
				if (countBytes)
				{
					byteCount += encoding.GetByteCount(new char[] { c });
				}
			}

			return ReadLineResult.Incomplete;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadLineResult ReadDelimiter(ref char c)
		{
			for (var i = delimiterPosition; i < delimiter.Length; i++)
			{
				if (bufferPosition >= charsRead)
				{
					return ReadLineResult.Incomplete;
				}

				delimiterPosition++;

				c = buffer[bufferPosition];
				if (c != delimiter[i])
				{
					c = buffer[bufferPosition - 1];
					delimiterPosition = 1;

					return ReadLineResult.Complete;
				}

				bufferPosition++;
				charCount++;
				if (countBytes)
				{
					byteCount += encoding.GetByteCount(new[] { c });
				}

				if (bufferPosition >= charsRead)
				{
					return ReadLineResult.Incomplete;
				}
			}

			AddField(fieldStartPosition, bufferPosition - fieldStartPosition - delimiter.Length);

			fieldStartPosition = bufferPosition;
			delimiterPosition = 1;

			return ReadLineResult.Complete;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadLineResult ReadLineEnding(ref char c)
		{
			var lessChars = 1;

			if (c == '\r')
			{
				if (bufferPosition >= charsRead)
				{
					return ReadLineResult.Incomplete;
				}

				c = buffer[bufferPosition];

				if (c == '\n')
				{
					lessChars++;
					bufferPosition++;
					charCount++;
					if (countBytes)
					{
						byteCount += encoding.GetByteCount(new char[] { c });
					}
				}
			}

			if (state == ParserState.LineEnding)
			{
				AddField(fieldStartPosition, bufferPosition - fieldStartPosition - lessChars);
			}

			return ReadLineResult.Complete;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ReadEndOfFile(in ReadLineResult result)
		{
			var state = this.state;
			this.state = ParserState.None;

			if (state == ParserState.BlankLine)
			{
				return false;
			}

			if (state == ParserState.Delimiter)
			{
				AddField(fieldStartPosition, bufferPosition - fieldStartPosition - delimiter.Length);

				fieldStartPosition = bufferPosition;

				AddField(fieldStartPosition, bufferPosition - fieldStartPosition);

				return true;
			}

			if (state == ParserState.LineEnding)
			{
				AddField(fieldStartPosition, bufferPosition - fieldStartPosition - 1);

				return true;
			}

			if (rowStartPosition < bufferPosition)
			{
				AddField(fieldStartPosition, bufferPosition - fieldStartPosition);
			}

			return fieldsPosition > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddField(in int start, in int length)
		{
			if (fieldsPosition >= fields.Length)
			{
				Array.Resize(ref fields, fields.Length * 2);
			}

			ref var field = ref fields[fieldsPosition];
			field.Start = start - rowStartPosition;
			field.Length = length;
			field.QuoteCount = quoteCount;

			fieldsPosition++;
			quoteCount = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool FillBuffer()
		{
			if (rowStartPosition == 0 && charCount > 0 && charsRead == bufferSize)
			{
				// The record is longer than the memory buffer. Increase the buffer.
				bufferSize *= 2;
				var tempBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
				buffer.CopyTo(tempBuffer, 0);
				ArrayPool<char>.Shared.Return(buffer);
				buffer = tempBuffer;
			}

			var charsLeft = Math.Max(charsRead - rowStartPosition, 0);

			Array.Copy(buffer, rowStartPosition, buffer, 0, charsLeft);

			fieldStartPosition -= rowStartPosition;
			rowStartPosition = 0;
			bufferPosition = charsLeft;

			charsRead = reader.Read(buffer, charsLeft, buffer.Length - charsLeft);
			if (charsRead == 0)
			{
				return false;
			}

			charsRead += charsLeft;

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private async Task<bool> FillBufferAsync()
		{
			if (rowStartPosition == 0 && charCount > 0 && charsRead == bufferSize)
			{
				// The record is longer than the memory buffer. Increase the buffer.
				bufferSize *= 2;
				var tempBuffer = ArrayPool<char>.Shared.Rent(bufferSize);
				buffer.CopyTo(tempBuffer, 0);
				ArrayPool<char>.Shared.Return(buffer);
				buffer = tempBuffer;
			}

			var charsLeft = Math.Max(charsRead - rowStartPosition, 0);

			Array.Copy(buffer, rowStartPosition, buffer, 0, charsLeft);

			fieldStartPosition -= rowStartPosition;
			rowStartPosition = 0;
			bufferPosition = charsLeft;

			charsRead = await reader.ReadAsync(buffer, charsLeft, buffer.Length - charsLeft);
			if (charsRead == 0)
			{
				return false;
			}

			charsRead += charsLeft;

			return true;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				// Dispose managed state (managed objects)
				ArrayPool<char>.Shared.Return(buffer);
				ArrayPool<char>.Shared.Return(processFieldBuffer);
			}

			// Free unmanaged resources (unmanaged objects) and override finalizer
			// Set large fields to null

			disposed = true;
		}

		/// <summary>
		/// Processes a raw field based on configuration.
		/// This will remove quotes, remove escapes, and trim if configured to.
		/// </summary>
		[DebuggerDisplay("Start = {Start}, Length = {Length}, Buffer.Length = {Buffer.Length}")]
		protected readonly ref struct ProcessedField
		{
			/// <summary>
			/// The start of the field in the buffer.
			/// </summary>
			public int Start { get; init; }

			/// <summary>
			/// The length of the field in the buffer.
			/// </summary>
			public int Length { get; init; }

			/// <summary>
			/// The buffer that contains the field.
			/// </summary>
			public char[] Buffer { get; init; }
		}

		private enum ReadLineResult
		{
			None = 0,
			Complete = 1,
			Incomplete = 2
		}

		private enum ParserState
		{
			None = 0,
			BlankLine = 1,
			Delimiter = 2,
			LineEnding = 3
		}

		[DebuggerDisplay("Start = {Start}, Length = {Length}, QuoteCount = {QuoteCount}")]
		private struct Field
		{
			/// <summary>
			/// Starting position of the field.
			/// This is an offset from <see cref="rowStartPosition"/>.
			/// </summary>
			public int Start;

			public int Length;

			public int QuoteCount;
		}
	}
}
