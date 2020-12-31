using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Performance
{
	public class CsvStackParser : IDisposable
	{
		private bool disposed;
		private TextReader reader;
		private Memory<char> memory = new Memory<char>();
		private int bufferPosition;
		private int bufferSize;
		private int charsRead;
		private int c = -1;
		private char quote;
		private char escape;
		private string delimiter;
		private int delimiterFirstChar;
		private string newLine = string.Empty;
		private int newLineFirstChar = -2;
		private bool leaveOpen;
		private CsvConfiguration configuration;
		private StringBuilder field = new StringBuilder();
		private int fieldStart;
		private int fieldLength;
		private int rawRecordStart;
		private int rawRecordLength;
		private List<string> record = new List<string>(128);

		public CsvConfiguration Configuration => configuration;

		public int Row { get; protected set; }

		public int RawRow { get; protected set; }

		public string RawRecord => new string(memory.Span.Slice(rawRecordStart, rawRecordLength));

		public string[] Record => record.ToArray();

		public string this[int index]
		{
			get
			{
				return record[index].ToString();
			}
		}

		public CsvStackParser(TextReader reader, CultureInfo culture) : this(reader, culture, false) { }

		public CsvStackParser(TextReader reader, CultureInfo culture, bool leaveOpen) : this(reader, new CsvConfiguration(culture)) { }

		public CsvStackParser(TextReader reader, CsvConfiguration configuration) : this(reader, configuration, false) { }

		public CsvStackParser(TextReader reader, CsvConfiguration configuration, bool leaveOpen)
		{
			this.reader = reader;
			this.configuration = configuration;
			this.leaveOpen = leaveOpen;
			bufferSize = configuration.BufferSize;
			quote = configuration.Quote;
			escape = configuration.Escape;
			delimiter = configuration.Delimiter;
			delimiterFirstChar = delimiter[0];
		}

		public bool Read()
		{
			Row++;
			RawRow++;
			record.Clear();
			rawRecordStart = bufferPosition;
			rawRecordLength = 0;

			var stackBuffer = memory.Span.Slice(0);

			while (true)
			{
				c = GetChar(ref stackBuffer);
				if (c == -1)
				{
					// End of file.
					return false;
				}

				if (c == quote)
				{
					if (ReadQuotedField(ref stackBuffer))
					{
						break;
					}
				}
				else
				{
					if (ReadField(ref stackBuffer))
					{
						break;
					}
				}
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected bool ReadField(ref Span<char> stackBuffer)
		{
			while (true)
			{
				if (c == delimiterFirstChar)
				{
					if (ReadDelimiter(ref stackBuffer))
					{
						return false;
					}
				}
				else if (c == newLineFirstChar || c == '\r' || c == '\n')
				{
					if (ReadLineEnding(ref stackBuffer))
					{
						return true;
					}
				}

				c = GetChar(ref stackBuffer);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected bool ReadQuotedField(ref Span<char> stackBuffer)
		{
			// `a!`b`,`c`
			fieldStart = bufferPosition;
			fieldLength = 0;
			var inQuotes = true;

			while (true)
			{
				c = GetChar(ref stackBuffer);

				if (c == escape && PeekChar(ref stackBuffer) == quote)
				{
					fieldLength--;
					AppendField(ref stackBuffer);
					c = GetChar(ref stackBuffer);
					continue;
				}
				else if (c == quote)
				{
					inQuotes = false;
					fieldLength--;
					AppendField(ref stackBuffer);
					continue;
				}

				if (!inQuotes)
				{
					if (c == delimiterFirstChar)
					{
						if (ReadDelimiter(ref stackBuffer))
						{
							return false;
						}
					}
					else if (c == newLineFirstChar || c == '\r' || c == '\n')
					{
						if (ReadLineEnding(ref stackBuffer))
						{
							return true;
						}
					}
				}
			}
		}

		protected bool ReadDelimiter(ref Span<char> stackBuffer)
		{
			Debug.Assert(c == delimiter[0], "Tried reading a delimiter when the first delimiter char didn't match the current char.");

			if (delimiter.Length > 1)
			{
				for (var i = 1; i < delimiter.Length; i++)
				{
					c = GetChar(ref stackBuffer);
					if (c != delimiter[i])
					{
						return false;
					}
				}
			}

			fieldLength -= delimiter.Length;

			AppendField(ref stackBuffer);

			record.Add(field.ToString());
			field = new StringBuilder();

			return true;
		}

		protected bool ReadLineEnding(ref Span<char> stackBuffer)
		{
			Debug.Assert((newLine.Length > 0 && c != newLine[0]) || c != '\r' || c != '\n', "Tried reading a line ending when the first delimiter char didn't match the current char and wasn't \\r or \\n.");

			if (newLine.Length > 0 && c == newLine[0])
			{
				if (newLine.Length == 1)
				{
					return true;
				}

				for (var i = 1; i < newLine.Length; i++)
				{
					c = GetChar(ref stackBuffer);
					if (c != newLine[i])
					{
						return false;
					}
				}

				fieldLength -= newLine.Length;
				return true;
			}
			else if (c == '\r')
			{
				fieldLength--;

				if (PeekChar(ref stackBuffer) == '\n')
				{
					c = GetChar(ref stackBuffer);
					fieldLength--;
				}
			}
			else // \n
			{
				fieldLength--;
			}

			AppendField(ref stackBuffer);

			record.Add(field.ToString());
			field = new StringBuilder();

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void AppendField(ref Span<char> stackBuffer)
		{
			field.Append(new string(stackBuffer.Slice(fieldStart, fieldLength)));
			fieldStart = bufferPosition;
			fieldLength = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected int GetChar(ref Span<char> stackBuffer)
		{
			if (!FillBuffer(ref stackBuffer))
			{
				return -1;
			}

			var c = stackBuffer[bufferPosition];
			bufferPosition++;
			fieldLength++;
			rawRecordLength++;

			return c;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected int PeekChar(ref Span<char> stackBuffer)
		{
			if (bufferPosition < charsRead)
			{
				return stackBuffer[bufferPosition];
			}

			return reader.Peek();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected bool FillBuffer(ref Span<char> stackBuffer)
		{
			// The buffer doesn't need to be filled yet.
			if (bufferPosition < charsRead)
			{
				return true;
			}

			var charsUsed = rawRecordStart;
			var bufferLeft = charsRead - charsUsed;
			var bufferUsed = charsRead - bufferLeft;

			var tempBuffer = new char[bufferLeft + bufferSize];
			stackBuffer.Slice(charsUsed).CopyTo(tempBuffer);
			charsRead = reader.Read(tempBuffer, bufferLeft, bufferSize);
			if (charsRead == 0)
			{
				return false;
			}

			charsRead += bufferLeft;

			memory = new Memory<char>(tempBuffer);
			stackBuffer = memory.Span.Slice(0);

			bufferPosition = bufferPosition - bufferUsed;
			fieldStart = fieldStart - bufferUsed;
			rawRecordStart = rawRecordStart - bufferUsed;

			return true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				if (!leaveOpen)
				{
					reader.Dispose();
				}
			}

			disposed = true;
		}
	}
}
