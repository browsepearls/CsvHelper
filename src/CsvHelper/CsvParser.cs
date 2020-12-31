using CsvHelper.Configuration;
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
	/// Parses CSV text.
	/// </summary>
	public class CsvParser : IParser
	{
		private readonly bool leaveOpen;
		private readonly CsvConfiguration configuration;
		private readonly TextReader reader;
		private bool disposed;
		private int bufferSize = -1;
		private int charsRead = -1;
		private IMemoryOwner<char> memoryOwner;
		private Memory<char> memory;
		private ReadOnlySequence<char> sequence;
		private int rowStartPosition;
		private int rowLength;
		private char quote;
		private char escape;
		private string delimiter;
		private char delimiterFirstChar;
		private FieldCollection fields = new FieldCollection();

		/// <summary>
		/// Gets the reading context.
		/// </summary>
		public ReadingContext Context { get; protected set; }

		/// <summary>
		/// Gets the number of fields for the current row.
		/// </summary>
		public int Count => fields.Count;

		/// <summary>
		/// Gets the CSV row the parser is currently on.
		/// </summary>
		public int Row { get; protected set; }

		/// <summary>
		/// Gets the raw row the parser is currently on.
		/// </summary>
		public int RawRow { get; protected set; }

		/// <summary>
		/// Gets the raw record for the current row.
		/// </summary>
		public string RawRecord => new string(memory.Slice(rowStartPosition, rowLength).Span);

		/// <summary>
		/// Gets the record for the current row.
		/// Note:
		/// It is much more efficient to only get the fields you need.
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
		/// Gets the field at the specified index for the current row.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The field.</returns>
		public string this[int index]
		{
			get
			{
				var position = fields[index];
				var length = position.Length;

				var value = string.Create(length, position, (Span<char> span, Field field) =>
				{
					var spanStart = 0;
					for (var i = 0; i < field.Count; i++)
					{
						var part = field[i];
						memory.Slice(part.Start, part.Length).Span.CopyTo(span.Slice(spanStart));
						spanStart += part.Length;
					}
				});

				return value;
			}
		}

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public IParserConfiguration Configuration => configuration;

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvParser"/> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <param name="leaveOpen">True to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser(TextReader reader, CultureInfo cultureInfo, bool leaveOpen = false) : this(reader, new CsvConfiguration(cultureInfo), leaveOpen) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvParser"/> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">True to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser(TextReader reader, CsvConfiguration configuration, bool leaveOpen = false)
		{
			Context = new ReadingContext(reader, configuration, leaveOpen);

			this.reader = reader;
			this.leaveOpen = leaveOpen;
			this.configuration = configuration;

			bufferSize = configuration.BufferSize;
			delimiter = configuration.Delimiter;
			delimiterFirstChar = configuration.Delimiter[0];
			escape = configuration.Escape;
			quote = configuration.Quote;
		}

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>True if there are more records to read, otherwise false.</returns>
		public bool Read()
		{
			Row++;
			rowStartPosition = rowStartPosition + rowLength;
			rowLength = 0;
			fields.Clear();

			var sequenceReader = new SequenceReader<char>();
			if (!FillSequence(ref sequenceReader))
			{
				return false;
			}

			while (true)
			{
				fields.Add();
				fields.Current.Add();

				fields.Current.Current.Start = sequenceReader.Position.GetInteger();

				if (!TryPeekChar(out var c, ref sequenceReader))
				{
					// EOF
					return false;
				}

				if (c == quote)
				{
					if (ReadQuotedField(ref sequenceReader))
					{
						break;
					}
				}
				else
				{
					if (ReadField(ref sequenceReader))
					{
						break;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Reads a record from the CSV file asynchronously.
		/// </summary>
		/// <returns>True if there are more records to read, otherwise false.</returns>
		public Task<bool> ReadAsync() => throw new NotImplementedException();

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				// Free managed resources.
				if (!leaveOpen)
				{
					reader.Dispose();
				}
			}

			disposed = true;
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ReadField(ref SequenceReader<char> sequenceReader)
		{
			while (true)
			{
				if (!TryGetChar(out var c, ref sequenceReader))
				{
					// EOF
					return true;
				}

				if (c == delimiterFirstChar)
				{
					if (ReadDelimiter(ref sequenceReader))
					{
						return false;
					}

					// Not a delimiter. Keep going.
				}
				else if (c == '\r' || c == '\n')
				{
					if (ReadLineEnding(c, ref sequenceReader))
					{
						return true;
					}

					// Not a line ending. Keep going.
				}
			}
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ReadQuotedField(ref SequenceReader<char> sequenceReader)
		{
			if (!TryGetChar(out var c, ref sequenceReader))
			{
				// EOF
				return false;
			}

			var inQuotes = true;

			while (true)
			{
				if (!TryGetChar(out c, ref sequenceReader))
				{
					// EOF
					return false;
				}

				if (c == escape)
				{
					if (!TryPeekChar(out var cPeek, ref sequenceReader))
					{
						// EOF
						return false;
					}

					if (cPeek == quote)
					{
						// Escaped quote was found. Keep going.
						continue;
					}
				}

				if (c == quote)
				{
					inQuotes = false;
					continue;
				}

				if (!inQuotes)
				{
					if (c == delimiterFirstChar)
					{
						if (ReadDelimiter(ref sequenceReader))
						{
							return false;
						}
					}
					else if (c == '\r' || c == '\n')
					{
						if (ReadLineEnding(c, ref sequenceReader))
						{
							return true;
						}
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ReadDelimiter(ref SequenceReader<char> sequenceReader)
		{
			if (delimiter.Length > 1)
			{
				for (var i = 1; i < delimiter.Length; i++)
				{
					if (!TryGetChar(out var c, ref sequenceReader) || c != delimiter[i])
					{
						return false;
					}
				}
			}

			fields.Current.Current.Length = sequenceReader.Position.GetInteger() - delimiter.Length - fields.Current.Current.Start;

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ReadLineEnding(char c, ref SequenceReader<char> sequenceReader)
		{
			var lineEndingCharCount = 1;
			if (c == '\r')
			{
				lineEndingCharCount++;

				if (!TryPeekChar(out var cPeek, ref sequenceReader))
				{
					fields.Current.Current.Length = sequenceReader.Position.GetInteger() - fields.Current.Current.Start;

					// EOF
					return true;
				}

				if (cPeek == '\n')
				{
					sequenceReader.Advance(1);
					rowLength++;
				}
			}

			fields.Current.Current.Length = sequenceReader.Position.GetInteger() - lineEndingCharCount - fields.Current.Current.Start;

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool TryGetChar(out char c, ref SequenceReader<char> sequenceReader)
		{
			if (sequenceReader.End && !FillSequence(ref sequenceReader))
			{
				// EOF
				c = '\0';

				return false;
			}

			sequenceReader.TryRead(out c);
			rowLength++;

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool TryPeekChar(out char c, ref SequenceReader<char> sequenceReader)
		{
			if (sequenceReader.End && !FillSequence(ref sequenceReader))
			{
				// EOF
				c = '\0';

				return false;
			}

			sequenceReader.TryPeek(out c);

			return true;
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool FillSequence(ref SequenceReader<char> sequenceReader)
		{
			Debug.Assert(sequenceReader.End, "The SequenceReader must be empty to fill it.");

			if (rowStartPosition + rowLength >= charsRead)
			{
				// We only need to fill the buffer if we've read everything from it.
				if (!FillBuffer())
				{
					return false;
				}
			}

			var start = rowStartPosition + rowLength;
			var length = charsRead - start;

			sequence = new ReadOnlySequence<char>(memory.Slice(rowStartPosition + rowLength, length));
			sequenceReader = new SequenceReader<char>(sequence);

			return true;
		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool FillBuffer()
		{
			Debug.Assert(rowStartPosition + rowLength >= charsRead, "The buffer must be empty to fill it.");

			if (memoryOwner == null)
			{
				memoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
				memory = memoryOwner.Memory;
				charsRead = reader.Read(memory.Span);
			}
			else
			{
				// If the row is longer than the buffer, make the buffer larger.
				if (rowStartPosition == 0)
				{
					bufferSize = memory.Length * 2;
				}

				// Copy the remainder of the row onto the new buffer.
				var tempMemoryOwner = MemoryPool<char>.Shared.Rent(bufferSize);
				var tempMemory = tempMemoryOwner.Memory;
				memory.Slice(rowStartPosition).CopyTo(tempMemory);
				var start = memory.Length - rowStartPosition;
				charsRead = reader.Read(tempMemory.Slice(start).Span);
				if (charsRead == 0)
				{
					return false;
				}

				charsRead += start;

				rowStartPosition = 0;

				memory = tempMemory;
				memoryOwner.Dispose();
				memoryOwner = tempMemoryOwner;
			}

			return true;
		}
	}
}
