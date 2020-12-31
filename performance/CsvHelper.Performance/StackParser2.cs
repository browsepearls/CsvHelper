using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Performance
{
	public class StackParser2 : IDisposable
	{
		private int bufferSize = -1;
		private readonly TextReader reader;
		private IMemoryOwner<char> memoryOwner;
		private int memoryPosition = -1;
		private string delimiter = ",";
		private int delimiterFirstChar = ',';
		private char quote = '"';
		private bool disposedValue;
		private readonly List<Field> fields = new List<Field>(128);
		private int charsRead;
		private int rowStartPosition; // Position in memory.
		private int fieldStartPosition; // Position in memory.

		public string[] Record
		{
			get
			{
				var length = 0;
				for (var i = 0; i < fields.Count; i++)
				{
					length += fields[i].Length;
				}

				var record = new string[fields.Count];

				for (var i = 0; i < fields.Count; i++)
				{
					record[i] = memoryOwner.Memory.Slice(fields[i].Start, fields[i].Length).Span.ToString();
				}

				return record;
			}
		}

		public StackParser2(TextReader reader)
		{
			this.reader = reader;
			//bufferSize = 2;
			bufferSize = 1024;
		}

		public bool Read()
		{
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

			while (true)
			{
				memoryPosition++;
				spanPosition++;

				if (memoryPosition == charsRead)
				{
					if (!FillBuffer())
					{
						return false;
					}

					span = memoryOwner.Memory.Slice(memoryPosition).Span;
					spanPosition = 0;
				}

				var c = span[spanPosition];

				if (inLineEnding)
				{
					var lineEndingChars = c == '\n' ? 1 : 0;

					fields.Add(new Field
					{
						IsQuoted = isQuoted,
						Start = fieldStartPosition - rowStartPosition,
						Length = memoryPosition - lineEndingChars - fieldStartPosition,
					});

					return memoryPosition < charsRead;
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
						}

						continue;
					}

					// This was not actually a delimiter.
					// Keep parsing as normal.
					inDelimiter = false;
					delimiterPosition = 0;
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

					continue;
				}
				else if (c == '\r')
				{
					inLineEnding = true;

					continue;
				}
				else if (c == '\n')
				{
					// End of line.
					fields.Add(new Field
					{
						IsQuoted = isQuoted,
						Start = fieldStartPosition - rowStartPosition,
						Length = memoryPosition - 1 - fieldStartPosition,
					});

					return charsRead < memoryOwner.Memory.Length;
				}
			}
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

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects)
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~StackParser2()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		private record Field
		{
			public bool IsQuoted { get; set; }

			/// <summary>
			/// Gets or sets the start position for this field.
			/// This value is relative to the row start position.
			/// </summary>
			public int Start { get; set; }

			public int Length { get; set; }
		}
	}
}
