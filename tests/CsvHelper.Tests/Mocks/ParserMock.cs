// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
using System.Threading.Tasks;
using System.Globalization;

namespace CsvHelper.Tests.Mocks
{
	public class ParserMock : IParser, IEnumerable<string[]>
	{
		private readonly Queue<string[]> rows;
		private ReadingContext context;

		public ReadingContext Context => context;

		public IParserConfiguration Configuration { get; }

		public int Count => context.Record.Length;

		public string[] Record => context.Record;

		public string RawRecord => context.RawRecord;

		public int Row => context.Row;

		public int RawRow => context.RawRow;

		public string this[int index] => throw new NotImplementedException();

		public ParserMock()
		{
			context = new ReadingContext(new StringReader(string.Empty), new CsvConfiguration(CultureInfo.InvariantCulture), false);
			rows = new Queue<string[]>();
		}

		public ParserMock(Queue<string[]> rows)
		{
			context = new ReadingContext(new StringReader(string.Empty), new CsvConfiguration(CultureInfo.InvariantCulture), false);
			this.rows = rows;
		}

		public bool Read()
		{
			context.Row++;
			context.Record = rows.Dequeue();

			return rows.Count > 0;
		}

		public Task<bool> ReadAsync()
		{
			context.Row++;

			return Task.FromResult(rows.Count > 0);
		}

		public void Add(params string[] row)
		{
			rows.Enqueue(row);
		}

		public IEnumerator<string[]> GetEnumerator()
		{
			return rows.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Dispose()
		{
		}
	}
}
