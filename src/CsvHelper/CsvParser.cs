using CsvHelper.Configuration;
using System;
using System.Buffers;
using System.Collections.Generic;
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

		private int bufferSize = -1;
		private IMemoryOwner<char> memoryOwner;
		private bool disposed;
		private readonly List<Field> fields = new List<Field>(128);
		private int charsRead = -1;
		private int memoryPosition = -1;
		private int rowStartPosition; // Position in memory.
		private int fieldStartPosition; // Position in memory.

		/// <summary>
		/// Gets the number of fields for the current row.
		/// </summary>
		public int Count => fields.Count;

		/// <summary>
		/// Gets the CSV row the parser is currently on.
		/// </summary>
		public int Row { get; private set; }

		/// <summary>
		/// Gets the raw row the parser is currently on.
		/// </summary>
		public int RawRow { get; private set; }

		/// <summary>
		/// Gets the record for the current row.
		/// Note: It is much more efficient to only get the fields you need.
		/// If you need all fields, then use this.
		/// </summary>
		public string[] Record
		{
			get
			{
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
		public string RawRecord => memoryOwner.Memory.Slice(rowStartPosition, memoryPosition + 1 - rowStartPosition).Span.ToString();

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
		public CsvParser(TextReader reader, CultureInfo culture) : this(reader, new CsvConfiguration(culture), false) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser(TextReader reader, CultureInfo culture, bool leaveOpen) : this(reader, new CsvConfiguration(culture), leaveOpen) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="Configuration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvParser(TextReader reader, CsvConfiguration configuration) : this(reader, configuration, false) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="Configuration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser(TextReader reader, CsvConfiguration configuration, bool leaveOpen)
		{
			this.reader = reader;
			bufferSize = configuration.BufferSize;
			delimiter = configuration.Delimiter;
			delimiterFirstChar = delimiter[0];
			ignoreBlankLines = configuration.IgnoreBlankLines;
			comment = configuration.Comment;
			allowComments = configuration.AllowComments;
			quote = configuration.Quote;
			escape = configuration.Escape;
			trimOptions = configuration.TrimOptions;
			whiteSpaceChars = configuration.WhiteSpaceChars;
			badDataFound = configuration.BadDataFound;
			context = new ReadingContext(reader, configuration, leaveOpen);
			processField = configuration.ProcessField;
			preDequoteField = configuration.PreDequoteField;
			dequoteField = configuration.DequoteField;
			postDequoteField = configuration.PostDequoteField;

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
			if (charsRead == 0)
			{
				return false;
			}

			Row++;
			fields.Clear();

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

			while (true)
			{
				memoryPosition++;
				spanPosition++;

				if (memoryPosition >= charsRead)
				{
					// Buffer ran out.

					if (!FillBuffer())
					{
						// EOF

						if (inLineEnding && memoryPosition - 1 == rowStartPosition && ignoreBlankLines)
						{
							// Ignore line.
							return false;
						}

						if (fieldStartPosition < memoryPosition)
						{
							// There is still a field that needs to be stored.

							var lineEndingChars = inLineEnding ? 1 : 0;

							fields.Add(new Field
							{
								IsQuoted = isQuoted,
								Start = fieldStartPosition - rowStartPosition,
								Length = memoryPosition - lineEndingChars - fieldStartPosition,
							});

							fieldStartPosition = memoryPosition;

							return true;
						}

						return false;
					}

					span = memoryOwner.Memory.Slice(memoryPosition).Span;
					spanPosition = 0;
				}

				var c = span[spanPosition];

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
								Length = memoryPosition - delimiter.Length - fieldStartPosition,
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

				if (c == quote)
				{
					inQuotes = !inQuotes;
					isQuoted = true;
				}

				if (inQuotes)
				{
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
					RawRow++;
					inLineEnding = true;

					continue;
				}
				else if (c == '\n')
				{
					RawRow++;

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

		public void Dispose()
		{
			Dispose(!Context?.LeaveOpen ?? true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				// Dispose managed state (managed objects)
				reader.Dispose();
				memoryOwner.Dispose();
				context.Dispose();
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
	}
}
