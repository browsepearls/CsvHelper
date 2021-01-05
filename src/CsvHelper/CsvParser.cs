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
		//private int memoryPosition = -1;
		private int rowStartPosition; // Position in memory.
		private int fieldStartPosition; // Position in memory.
		private int row;
		private int rawRow;

		public long CharPosition => charCount;

		public long BytePosition => byteCount;

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

				return memoryOwner.Memory.Slice(rowStartPosition, memoryPosition + 1 - rowStartPosition).Span;
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
					Escape = escape,
					IsQuoted = fields[index].IsQuoted,
					Quote = quote,
					TrimOptions = trimOptions,
					WhiteSpaceChars = whiteSpaceChars,
					PreDequote = preDequoteField,
					Dequote = dequoteField,
					PostDequote = postDequoteField,
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
		/// <param name="leaveOpen"><c>true</c> to leave the <see cref="TextReader"/> open after the <see cref="CsvReader"/> object is disposed, otherwise <c>false</c>.</param>
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





			// Make sure the span size isn't bigger than the buffer size.
			spanBufferSize = Math.Min(1024, bufferSize);
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

			//if (charsRead == 0)
			//{
			//	return false;
			//}

			Span<char> span = Span<char>.Empty;
			if (memoryPosition > -1)
			{
				span = memoryOwner.Memory.Slice(memoryPosition + 1).Span;
				rowStartPosition = fieldStartPosition = memoryPosition + 1;
			}

			var spanPosition = -1;
			var inQuotes = false;
			var isQuoted = false;
			var inLineEnding = false;
			var inDelimiter = false;
			var delimiterPosition = 0;
			var inComment = false;
			var c = '\0';
			char cPrev;

			while (true)
			{
				charCount++;
				memoryPosition++;
				spanPosition++;

				if (memoryPosition >= charsRead)
				{
					// Buffer ran out.

					if (!FillBuffer())
					{
						// EOF

						if (row == 0)
						{
							row++;
						}

						if (rawRow == 0)
						{
							rawRow++;
						}

						if (countBytes)
						{
							byteCount += encoding.GetByteCount(RawRecord);
						}

						if (inLineEnding && memoryPosition - 1 == rowStartPosition && ignoreBlankLines)
						{
							// Ignore line.
							return false;
						}

						if (inComment && allowComments)
						{
							// Ignore line.
							return false;
						}

						if (rowStartPosition < memoryPosition)
						{
							// Add the last field.

							var lineEndingChars = inLineEnding ? 1 : 0;

							fields.Add(new Field
							{
								IsQuoted = isQuoted,
								Start = fieldStartPosition - rowStartPosition,
								Length = memoryPosition - lineEndingChars - fieldStartPosition,
							});

							fieldStartPosition = memoryPosition;
							memoryPosition--;

							return true;
						}

						return false;
					}

					span = memoryOwner.Memory.Slice(memoryPosition).Span;
					spanPosition = 0;
				}

				cPrev = c;
				c = span[spanPosition];

				if (c == '\r' || c == '\n' && cPrev != '\r')
				{
					rawRow++;
				}

				if (inLineEnding)
				{
					inLineEnding = false;

					var lineEndingChars = c == '\n' ? 1 : 0;

					if (memoryPosition - 1 == rowStartPosition && ignoreBlankLines)
					{
						// Blank line.
						// Skip the line and continue reading.
						rowStartPosition = memoryPosition + lineEndingChars;
						fieldStartPosition = rowStartPosition;

						// If the line ending has a '\n' then start loop over.
						// If not, this is a new field and the character needs
						// to be processed.
						if (c == '\n')
						{
							continue;
						}
					}
					else if (inComment)
					{
						// End of commented line.
						// Skip the line and continue reading.
						inComment = false;
						rowStartPosition = memoryPosition + lineEndingChars;
						fieldStartPosition = rowStartPosition;

						if (c == '\n')
						{
							continue;
						}
					}
					else
					{
						fields.Add(new Field
						{
							IsQuoted = isQuoted,
							Start = fieldStartPosition - rowStartPosition,
							Length = memoryPosition - 1 - fieldStartPosition,
						});

						if (c != '\n')
						{
							memoryPosition--;
						}

						if (countBytes)
						{
							byteCount += encoding.GetByteCount(RawRecord);
						}

						return true;
					}
				}

				if (inDelimiter)
				{
					delimiterPosition++;

					if (c == delimiter[delimiterPosition])
					{
						if (delimiterPosition + 1 >= delimiter.Length)
						{
							// End of delimiter.
							fields.Add(new Field
							{
								IsQuoted = isQuoted,
								Start = fieldStartPosition - rowStartPosition,
								Length = memoryPosition - fieldStartPosition - delimiterPosition,
							});

							inDelimiter = false;
							delimiterPosition = 0;
							fieldStartPosition = memoryPosition + 1;
							isQuoted = false;
						}

						continue;
					}

					// This was not actually a delimiter.
					// Keep parsing as normal.
					inDelimiter = false;
					delimiterPosition = 0;
				}

				if (inComment && c != '\r' && c != '\n')
				{
					// If we're on a commented line, nothing else is parsed.
					continue;
				}

				if (c == quote && !ignoreQuotes)
				{
					inQuotes = !inQuotes;
					isQuoted = true;
				}

				if (inQuotes)
				{
					if (lineBreakInQuotedFieldIsBadData && (c == '\r' || c == '\n'))
					{
						badDataFound?.Invoke(context);
					}

					// If we're in quotes, nothing else is parsed.
					continue;
				}

				if (c == comment && allowComments && memoryPosition == rowStartPosition)
				{
					// Skip commented row.
					inComment = true;

					continue;
				}

				if (c == delimiterFirstChar)
				{
					if (delimiter.Length > 1)
					{
						// There is more of the delimiter to read.
						inDelimiter = true;
						continue;
					}

					fields.Add(new Field
					{
						IsQuoted = isQuoted,
						Start = fieldStartPosition - rowStartPosition,
						Length = memoryPosition - fieldStartPosition,
					});

					fieldStartPosition = memoryPosition + 1;
					isQuoted = false;

					continue;
				}
				else if (c == '\r')
				{
					row++;
					inLineEnding = true;

					continue;
				}
				else if (c == '\n')
				{
					row++;

					if (inComment)
					{
						// End of commented line.
						// Skip the line and continue reading.
						inComment = false;
						rowStartPosition = memoryPosition + 1;
						fieldStartPosition = rowStartPosition;

						continue;
					}

					if (memoryPosition == rowStartPosition && ignoreBlankLines)
					{
						// Blank line.
						// Skip the line and continue reading.
						rowStartPosition = memoryPosition + 1;
						fieldStartPosition = rowStartPosition;

						continue;
					}

					// End of line.
					fields.Add(new Field
					{
						IsQuoted = isQuoted,
						Start = fieldStartPosition - rowStartPosition,
						Length = memoryPosition - fieldStartPosition,
					});

					if (countBytes)
					{
						byteCount += encoding.GetByteCount(RawRecord);
					}

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
		/// Fills the buffer.
		/// </summary>
		/// <returns>Value indicating if there is more to read.</returns>
		private bool FillBuffer()
		{
			if (memoryOwner == null)
			{
				memoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
				charsRead = reader.Read(memoryOwner.Memory.Span);

				return charsRead > 0;
			}

			// If the row is longer than the buffer, make the buffer larger.
			if (rowStartPosition == 0)
			{
				bufferSize = memoryOwner.Memory.Length * 2;
				//Console.Write($"{bufferSize} ");
			}

			// Copy the remainder of the row onto the new memory.
			var tempMemoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
			memoryOwner.Memory.Slice(rowStartPosition).CopyTo(tempMemoryOwner.Memory);
			var start = memoryOwner.Memory.Length - rowStartPosition;
			charsRead = reader.Read(tempMemoryOwner.Memory.Slice(start).Span);
			if (charsRead == 0)
			{
				return false;
			}

			charsRead += start;
			memoryPosition = start;
			fieldStartPosition = fieldStartPosition - rowStartPosition;
			// The memory copied was from the row start, so this is now
			// at the beginning of the memory.
			rowStartPosition = 0;

			memoryOwner.Dispose();
			memoryOwner = tempMemoryOwner;

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

			public int Start { get; init; }

			public int Length { get; init; }
		}


		private int memoryPosition = -1;

		private char c;
		private char cPrev;
		private int spanPosition;
		private int spanBufferSize = 1024;
		private int spanLength = 0;
		private bool inQuotes;

		public bool Read2()
		{
			fields.Clear();
			spanPosition = 0;
			spanLength = 0;
			inQuotes = false;
			rowStartPosition = memoryPosition + 1;
			fieldStartPosition = rowStartPosition;

			var span = Span<char>.Empty;

			while (true)
			{
				if (!NextChar(ref span))
				{
					return false;
				}

				if (c == quote)
				{
					inQuotes = !inQuotes;
				}

				if (inQuotes)
				{
					// White in quotes, we don't care about anything else.
					continue;
				}

				if (c == delimiterFirstChar)
				{
					if (!ReadDelimiter(ref span))
					{
						return true;
					}
				}

				if (c == '\r' || c == '\n')
				{
					ReadLineEnding(ref span);

					return true;
				}
			}
		}

		private void ReadBlankLine(ref Span<char> span)
		{
			while (true)
			{
				c = span[spanPosition];
			}
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
					return false;
				}
			}

			AddField(fieldStartPosition, memoryPosition - fieldStartPosition - delimiter.Length + 1);

			fieldStartPosition = memoryPosition + 1;

			return true;
		}

		/// <summary>
		/// Reads the line ending.
		/// </summary>
		/// <param name="span">The span.</param>
		/// <returns><c>true</c> if there is more to read, otherwise false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ReadLineEnding(ref Span<char> span)
		{
			row++;

			if (!NextChar(ref span))
			{
				// EOF
				return;
			}

			AddField(fieldStartPosition, memoryPosition - fieldStartPosition - 1);

			if (c != '\n')
			{
				// The char isn't part of the line ending.
				// Move positions back one char.
				memoryPosition--;
				spanPosition = spanPosition == 0 ? 0 : spanPosition - 1;
			}
		}

		private void ReadEof(ref Span<char> span)
		{
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddField(int start, int length)
		{
			fields.Add(new Field
			{
				Start = start - rowStartPosition,
				Length = length,
				IsQuoted = inQuotes,
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
				c = '\0';

				return false;
			}

			memoryPosition++;
			spanPosition++;

			cPrev = c;
			c = span[spanPosition];

			charCount++;

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
			if (memoryPosition + 1 >= charsRead)
			{
				// Fill memory buffer.

				if (charCount == 0)
				{
					// First read.
					memoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
					span = memoryOwner.Memory.Slice(0, spanBufferSize).Span;
					charsRead = reader.Read(span);

					memoryPosition = -1;
					spanPosition = -1;
					spanLength = span.Length;

					return charsRead > 0;
				}

				// If the row is longer than the buffer, make the buffer larger.
				if (rowStartPosition == 0 && charsRead > 0)
				{
					// maybe use charsread instead of memory.length
					bufferSize = Math.Max(memoryOwner.Memory.Length, bufferSize) * 2;
				}

				// Copy the remaining row onto the new memory.
				var tempMemoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
				memoryOwner.Memory.Slice(rowStartPosition).CopyTo(tempMemoryOwner.Memory);
				var start = memoryOwner.Memory.Length - rowStartPosition;
				charsRead = reader.Read(tempMemoryOwner.Memory.Slice(start).Span);
				if (charsRead == 0)
				{
					return false;
				}

				charsRead += start;
				memoryPosition = start - 1;
				fieldStartPosition = fieldStartPosition - rowStartPosition;
				rowStartPosition = 0;

				memoryOwner.Dispose();
				memoryOwner = tempMemoryOwner;
			}

			if (spanPosition + 1 >= spanLength)
			{
				// Fill span.
				var start = memoryPosition + 1;
				//spanBufferSize = 
				var length = Math.Min(spanBufferSize, charsRead - start);
				span = memoryOwner.Memory.Slice(start, length).Span;
				//Console.Write($"{length} ");

				spanPosition = -1;
				spanLength = span.Length;
			}

			return true;
		}
	}
}
