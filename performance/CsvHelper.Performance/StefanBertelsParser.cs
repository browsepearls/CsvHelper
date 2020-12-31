using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Performance
{
	public class StefanBartelsParserContext : IDisposable
	{
		internal IMemoryOwner<char> SingleLineOwner;
		internal int DelimiterSize = 1;
		internal ReadOnlyMemory<char> Line;
		internal List<int> DelimiterAfter;

		public int FieldCount => DelimiterAfter.Count;

		public ReadOnlyMemory<char> GetRawField(int colNo) => Line.Slice(colNo == 0 ? 0 : DelimiterAfter[colNo - 1] + DelimiterSize, DelimiterAfter[colNo] - (colNo == 0 ? 0 : DelimiterAfter[colNo - 1] + DelimiterSize));

		public void Dispose()
		{
			SingleLineOwner?.Dispose();
		}
	}

	public class StefanBertelsParser
	{
		private readonly TextReader _tr;
		private char[] _buffer = new char[2];
		private int _bufferPos = 0;
		private int _bufferLen = 0; // last pos + 1
		private bool _allRead = false;

		public StefanBertelsParser(TextReader tr, bool leaveOpen)
		{
			_tr = tr;
		}

		public IEnumerable<StefanBartelsParserContext> Read()
		{
			using (var context = new StefanBartelsParserContext())
			{
				context.DelimiterAfter = new List<int>(100);
				var bufferMinSize = 2;

				var bufferOwner = MemoryPool<char>.Shared.Rent(bufferMinSize);
				try
				{
					var buffer = bufferOwner.Memory;
					var processedBufferPos = 0;
					var bufferEndPos = 0;
					var eof = false;
					var readMoreDataNext = true;

					while (true)
					{
						if (readMoreDataNext)
						{
							var readBytes = _tr.Read(buffer.Slice(bufferEndPos).Span);
							eof |= readBytes == 0;
							bufferEndPos += readBytes;
						}

						if (eof) readMoreDataNext = false;

						if (processedBufferPos >= bufferEndPos) break;

						var newPos = ParseLine(new ReadOnlySequence<char>(buffer.Slice(processedBufferPos, bufferEndPos - processedBufferPos)), eof, context);

						if (newPos >= 0)
						{
							yield return context;
							readMoreDataNext = false;
							processedBufferPos += newPos;

							if (!eof && processedBufferPos >= buffer.Length)
							{
								// buffer fully processed, mark free
								bufferEndPos = 0;
								processedBufferPos = 0;
								readMoreDataNext = true;
							}
						}
						else
						{
							// need more bytes
							if (processedBufferPos > 0)
							{
								// shift 
								buffer.Slice(processedBufferPos, bufferEndPos - processedBufferPos).CopyTo(buffer);
								bufferEndPos -= processedBufferPos;
								processedBufferPos = 0;
							}
							else
							{
								// resize
								bufferMinSize = 1 + buffer.Length * 3 / 2;
								var newBufferOwner = MemoryPool<char>.Shared.Rent(bufferMinSize);
								var newBuffer = newBufferOwner.Memory;
								buffer.Slice(0, bufferEndPos).CopyTo(newBuffer);
								buffer = newBuffer;
								bufferOwner.Dispose();
								bufferOwner = newBufferOwner;
							}
							readMoreDataNext = true;
						}

						if (eof)
						{
							Debug.Assert(newPos == bufferEndPos);
							break;
						}
					}
				}
				finally
				{
					bufferOwner.Dispose();
				}
			}
		}

		public static int ParseLine(ReadOnlySequence<char> lineBuffer, bool eof, StefanBartelsParserContext context3)
		{
			var pos = 0; //  lineBuffer.Start.GetInteger();
			var isFirstSeq = true;
			var lineDelimiterRead = false;
			var lineDelimiterHalfRead = false;

			context3.Line = ReadOnlyMemory<char>.Empty;
			context3.SingleLineOwner?.Dispose();
			context3.SingleLineOwner = null;
			context3.DelimiterAfter.Clear();

			foreach (var seq in lineBuffer)
			{
				foreach (var c in seq.Span)
				{
					if (lineDelimiterHalfRead)
					{
						if (c != '\n') pos--;
						lineDelimiterHalfRead = false;
						lineDelimiterRead = true;
						break;
					}

					if (c == '\r')
					{
						if (isFirstSeq)
						{
							context3.DelimiterAfter.Add(pos);
							context3.Line = seq.Slice(0, pos);
						}
						lineDelimiterHalfRead = true;
					}
					else if (c == '\n')
					{
						if (isFirstSeq)
						{
							context3.DelimiterAfter.Add(pos);
							context3.Line = seq.Slice(0, pos);
						}
						lineDelimiterRead = true;
						break;
					}
					else if (c == ',') // delimiter
					{
						if (isFirstSeq)
						{
							context3.DelimiterAfter.Add(pos);
						}
					}
					pos++;
				}

				if (lineDelimiterRead) break;

				isFirstSeq = false;
			}

			if (lineDelimiterRead || eof)
			{
				if (!isFirstSeq)
				{
					// copy segments to new Memory buffer
					context3.SingleLineOwner = MemoryPool<char>.Shared.Rent(pos);
					var memory = context3.SingleLineOwner.Memory;
					lineBuffer.Slice(0, pos).CopyTo(memory.Span);
					context3.Line = memory;
				}

				return lineDelimiterRead ? pos + 1 : pos;
			}
			return -1;
		}
	}
}
