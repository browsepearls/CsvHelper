using CsvHelper.Configuration;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
		private readonly string delimiter = ",";
		private readonly int delimiterFirstChar = ',';
		private readonly char quote;
		private readonly char escape;
		private readonly bool ignoreBlankLines;
		private readonly char comment;
		private readonly bool allowComments;
		private readonly TrimOptions trimOptions;
		private readonly char[] whiteSpaceChars;
		private readonly ReadingContext context;
		private readonly Action<ReadingContext> badDataFound;
		private readonly ProcessFieldFunc processField;
		private readonly PreDequoteFieldFunc preDequoteField;
		private readonly DequoteFieldFunc dequoteField;
		private readonly PostDequoteFieldFunc postDequoteField;
		private readonly bool countBytes;
		private readonly Encoding encoding;
		private readonly bool ignoreQuotes;
		private readonly bool leaveOpen;
		private readonly bool lineBreakInQuotedFieldIsBadData;

		private long charCount;
		private long byteCount;
		private int bufferSize = -1;
		private IMemoryOwner<char> memoryOwner;
		private bool disposed;
		private readonly List<Field> fields = new List<Field>(128);
		private int charsRead = -1;
		private int memoryPosition;
		private int rowStartPosition; // Position in memory.
		private int fieldStartPosition; // Position in memory.
		private int row;
		private int rawRow;
		private char c;
		private char cPrev;
		private int spanPosition;
		private bool inQuotes;
		private bool isQuoted;

		/// <summary>
		/// Gets the count of how many characters have been read.
		/// </summary>
		public long CharCount => charCount;

		/// <summary>
		/// Gets the count of how many bytes have been read.
		/// <see cref="IParserConfiguration.CountBytes"/> needs
		/// to be enabled for this value to be populated.
		/// </summary>
		public long ByteCount => byteCount;

		/// <summary>
		/// Gets the number of fields for the current row.
		/// </summary>
		public int Count => fields.Count;

		/// <summary>
		/// Gets the CSV row the parser is currently on.
		/// </summary>
		public int Row => row;

		/// <summary>
		/// Gets the raw row the parser is currently on.
		/// </summary>
		public int RawRow => rawRow;

		/// <summary>
		/// Gets the record for the current row.
		/// Note: It is much more efficient to only get the fields you need.
		/// If you need all fields, then use this.
		/// </summary>
		public string[] Record
		{
			get
			{
				// TODO: Cache the current record

				if (fields.Count == 0)
				{
					return null;
				}

				var record = new string[fields.Count];

				for (var i = 0; i < fields.Count; i++)
				{
					record[i] = this[i];
				}

				return record;
			}
		}

		/// <summary>
		/// Gets the raw record for the current row.
		/// </summary>
		public Span<char> RawRecord
		{
			get
			{
				if (fields.Count == 0)
				{
					return null;
				}

				return memoryOwner.Memory.Slice(rowStartPosition, memoryPosition - rowStartPosition).Span;
			}
		}

		/// <summary>
		/// Gets the reading context.
		/// </summary>
		public ReadingContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public IParserConfiguration Configuration { get; private set; }

		/// <summary>
		/// Gets the field at the specified index for the current row.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The field.</returns>
		public string this[int index]
		{
			get
			{
				var options = new ProcessFieldOptions
				{
					BadDataFound = badDataFound,
					Context = context,
					Dequote = dequoteField,
					Escape = escape,
					IgnoreQuotes = ignoreQuotes,
					IsQuoted = fields[index].IsQuoted,
					LineBreakInQuotedFieldIsBadData = lineBreakInQuotedFieldIsBadData,
					PreDequote = preDequoteField,
					PostDequote = postDequoteField,
					Quote = quote,
					TrimOptions = trimOptions,
					WhiteSpaceChars = whiteSpaceChars,
				};

				var span = memoryOwner.Memory.Slice(fields[index].Start + rowStartPosition, fields[index].Length).Span;
				span = processField(span, options);

				return span.ToString();
			}
		}

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen"><c>true</c> to leave the <see cref="TextReader"/> open after the <see cref="CsvParser"/> object is disposed, otherwise <c>false</c>.</param>
		public CsvParser(TextReader reader, CultureInfo culture, bool leaveOpen = false) : this(reader, new CsvConfiguration(culture) { LeaveOpen = leaveOpen }) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="Configuration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvParser(TextReader reader, CsvConfiguration configuration)
		{
			this.reader = reader;

			allowComments = configuration.AllowComments;
			badDataFound = configuration.BadDataFound;
			bufferSize = configuration.BufferSize;
			comment = configuration.Comment;
			context = new ReadingContext(this);
			countBytes = configuration.CountBytes;
			delimiter = configuration.Delimiter;
			delimiterFirstChar = delimiter[0];
			dequoteField = configuration.DequoteField;
			encoding = configuration.Encoding;
			escape = configuration.Escape;
			ignoreBlankLines = configuration.IgnoreBlankLines;
			ignoreQuotes = configuration.IgnoreQuotes;
			leaveOpen = configuration.LeaveOpen;
			lineBreakInQuotedFieldIsBadData = configuration.LineBreakInQuotedFieldIsBadData;
			postDequoteField = configuration.PostDequoteField;
			preDequoteField = configuration.PreDequoteField;
			processField = configuration.ProcessField;
			quote = configuration.Quote;
			trimOptions = configuration.TrimOptions;
			whiteSpaceChars = configuration.WhiteSpaceChars;

			Configuration = configuration;
		}

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>
		/// True if there are more records to read, otherwise false.
		/// </returns>
		public bool Read()
		{
			fields.Clear();
			spanPosition = 0;
			inQuotes = false;
			isQuoted = false;
			rowStartPosition = memoryPosition;
			fieldStartPosition = rowStartPosition;

			var span = Span<char>.Empty;

			while (true)
			{
				if (!NextChar(ref span))
				{
					return ReadEndOfFile(ref span);
				}

				if (c == comment && allowComments || rowStartPosition == memoryPosition - 1 && (c == '\r' || c == '\n') && ignoreBlankLines)
				{
					ReadBlankLine(ref span);

					continue;
				}

				if (c == quote)
				{
					isQuoted = true;

					if (!ignoreQuotes)
					{
						inQuotes = !inQuotes;
					}
				}

				if (inQuotes)
				{
					if (c == '\r' || c == '\n' && cPrev != '\r')
					{
						rawRow++;
					}

					// White in quotes, we don't care about anything else.
					continue;
				}

				if (c == delimiterFirstChar)
				{
					if (ReadDelimiter(ref span))
					{
						continue;
					}
				}

				if (c == '\r' || c == '\n')
				{
					ReadLineEnding(ref span);

					return true;
				}
			}
		}

		/// <summary>
		/// Reads a record from the CSV file asynchronously.
		/// </summary>
		/// <returns>
		/// True if there are more records to read, otherwise false.
		/// </returns>
		public Task<bool> ReadAsync()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Reads the blank line.
		/// </summary>
		/// <param name="span">The span.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ReadBlankLine(ref Span<char> span)
		{
			// Read until a line ending is found.
			while (true)
			{
				if (c == '\r' || c == '\n')
				{
					NextChar(ref span);

					if (c != '\n')
					{
						memoryPosition--;
						spanPosition = spanPosition == 0 ? 0 : spanPosition - 1;
						charCount--;

						if (countBytes)
						{
							byteCount -= encoding.GetByteCount(new[] { c });
						}
					}

					break; ;
				}

				if (!NextChar(ref span))
				{
					break;
				}
			}

			rowStartPosition = memoryPosition;
			fieldStartPosition = rowStartPosition;
			row++;
			rawRow++;
			isQuoted = false;
		}

		/// <summary>
		/// Reads the delimiter.
		/// </summary>
		/// <param name="span">The span.</param>
		/// <returns><c>true</c> if this was a delimiter, othersize <c>false</c>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ReadDelimiter(ref Span<char> span)
		{
			for (var i = 1; i < delimiter.Length; i++)
			{
				NextChar(ref span);

				if (c != delimiter[i])
				{
					memoryPosition--;
					spanPosition = spanPosition == 0 ? 0 : spanPosition - 1;
					charCount--;

					if (countBytes)
					{
						byteCount -= encoding.GetByteCount(new[] { c });
					}

					return false;
				}
			}

			AddField(fieldStartPosition, memoryPosition - fieldStartPosition - delimiter.Length);

			fieldStartPosition = memoryPosition;
			isQuoted = false;

			return true;
		}

		/// <summary>
		/// Reads the line ending.
		/// </summary>
		/// <param name="span">The span.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ReadLineEnding(ref Span<char> span)
		{
			row++;
			rawRow++;

			var hasMoreChars = NextChar(ref span);
			var charsToRemove = hasMoreChars ? 2 : 1;

			AddField(fieldStartPosition, memoryPosition - fieldStartPosition - charsToRemove);

			if (c != '\n' && hasMoreChars)
			{
				// The char isn't part of the line ending.
				// Move positions back one char.
				memoryPosition--;
				spanPosition = spanPosition == 0 ? 0 : spanPosition - 1;
				charCount--;

				if (countBytes)
				{
					byteCount -= encoding.GetByteCount(new[] { c });
				}
			}
		}

		/// <summary>
		/// Reads the end of file.
		/// </summary>
		/// <param name="span">The span.</param>
		/// <returns><c>true</c> if there is more to read, otherwise <c>false</c>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ReadEndOfFile(ref Span<char> span)
		{
			if (fieldStartPosition < memoryPosition || fields.Count > 0)
			{
				row++;
				rawRow++;

				AddField(fieldStartPosition, memoryPosition - fieldStartPosition);

				return true;
			}

			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddField(int start, int length)
		{
			fields.Add(new Field
			{
				Start = start - rowStartPosition,
				Length = length,
				IsQuoted = isQuoted,
			});
		}

		/// <summary>
		/// Gets the next char and sets it to <see cref="c"/>.
		/// </summary>
		/// <param name="span">The span.</param>
		/// <returns><c>true</c> if there are more characters, otherwise <c>false</c>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool NextChar(ref Span<char> span)
		{
			if (!FillBuffers(ref span))
			{
				return false;
			}

			cPrev = c;
			c = span[spanPosition];

			memoryPosition++;
			spanPosition++;
			charCount++;

			if (countBytes)
			{
				byteCount += encoding.GetByteCount(new[] { c });
			}

			return true;
		}

		/// <summary>
		/// Fills the memory and span buffers.
		/// </summary>
		/// <param name="span">The span to fill.</param>
		/// <returns><c>true</c> if there is more to read, otherwise <c>false</c>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool FillBuffers(ref Span<char> span)
		{
			if (memoryPosition >= charsRead)
			{
				// Fill memory buffer.

				if (charCount == 0)
				{
					// First read.
					memoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
					span = memoryOwner.Memory.Slice(0, Math.Min(1024, memoryOwner.Memory.Length)).Span;
					charsRead = reader.Read(span);

					memoryPosition = 0;
					spanPosition = 0;

					return charsRead > 0;
				}

				// If the row is longer than the buffer, make the buffer larger.
				if (rowStartPosition == 0 && charsRead > 0)
				{
					// TODO: maybe use charsread instead of memory.length
					bufferSize = Math.Max(charsRead, bufferSize) * 2;
				}

				// Copy the remaining row onto the new memory.
				var tempMemoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
				var charsLeft = Math.Max(charsRead - rowStartPosition, 0);
				memoryOwner.Memory.Slice(rowStartPosition, charsLeft).CopyTo(tempMemoryOwner.Memory);
				charsRead = reader.Read(tempMemoryOwner.Memory.Slice(charsLeft).Span);
				if (charsRead == 0)
				{
					return false;
				}

				charsRead += charsLeft;
				memoryPosition = charsLeft;
				fieldStartPosition = fieldStartPosition - rowStartPosition;
				rowStartPosition = 0;

				memoryOwner.Dispose();
				memoryOwner = tempMemoryOwner;
			}

			if (spanPosition >= span.Length)
			{
				// Fill span.
				var start = memoryPosition;
				var length = Math.Min(1024, charsRead - start);
				span = memoryOwner.Memory.Slice(start, length).Span;

				spanPosition = 0;
			}

			return true;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				// Dispose managed state (managed objects)
				if (!leaveOpen)
				{
					reader?.Dispose();
				}

				memoryOwner?.Dispose();
			}

			// Free unmanaged resources (unmanaged objects) and override finalizer
			// Set large fields to null

			disposed = true;
		}

		private record Field
		{
			public bool IsQuoted { get; init; }

			/// <summary>
			/// The start position of the field.
			/// This position is an offset from the <see cref="rowStartPosition"/>.
			/// </summary>
			public int Start { get; init; }

			public int Length { get; init; }
		}
	}
}
