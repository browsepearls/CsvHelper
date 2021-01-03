// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
	public class BadDataTests
	{
		[TestMethod]
		public void CallbackTest()
		{
			string field = null;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = f => field = f.Field,
			};
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine(" a\"bc\",d");
				writer.WriteLine("\"a\"\"b\"c \" ,d");
				writer.WriteLine("\"a\"\"b\",c");
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.IsNotNull(field);
				Assert.AreEqual(" a\"bc\"", field);

				field = null;
				parser.Read();
				Assert.IsNotNull(field);
				Assert.AreEqual("a\"bc \" ", field);

				field = null;
				parser.Read();
				Assert.IsNull(field);
			}
		}

		[TestMethod]
		public void ThrowExceptionTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("1,2");
				writer.WriteLine(" a\"bc\",d");
				writer.Flush();
				stream.Position = 0;

				parser.Read();
				try
				{
					parser.Read();
					Assert.Fail("Failed to throw exception on bad data.");
				}
				catch (BadDataException) { }
			}
		}

		[TestMethod]
		public void IgnoreQuotesTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				IgnoreQuotes = true,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				writer.WriteLine("one,2\"two,three");
				writer.Flush();
				stream.Position = 0;

				parser.Read();

				Assert.AreEqual("2\"two", parser[1]);
			}
		}

		[TestMethod]
		public void LineBreakInQuotedFieldIsBadDataCrTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				LineBreakInQuotedFieldIsBadData = true,
			};
			using (var reader = new StringReader("\"a\rb\""))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.ThrowsException<BadDataException>(() => parser.Read());
			}
		}

		[TestMethod]
		public void LineBreakInQuotedFieldIsBadDataLfTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				LineBreakInQuotedFieldIsBadData = true,
			};
			using (var reader = new StringReader("\"a\nb\""))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.ThrowsException<BadDataException>(() => parser.Read());
			}
		}

		[TestMethod]
		public void LineBreakInQuotedFieldIsBadDataCrLfTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				LineBreakInQuotedFieldIsBadData = true,
			};
			using (var reader = new StringReader("\"a\r\nb\""))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.ThrowsException<BadDataException>(() => parser.Read());
			}
		}
	}
}
