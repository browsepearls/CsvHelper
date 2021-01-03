// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using System.IO;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ExcelCompatibleTests
	{
		[TestMethod]
		public void ParseTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("one,two,three");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("one", parser[0]);
				Assert.AreEqual("two", parser[1]);
				Assert.AreEqual("three", parser[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldsTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				// "one","two","three"
				writer.WriteLine("\"one\",\"two\",\"three\"");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("one", parser[0]);
				Assert.AreEqual("two", parser[1]);
				Assert.AreEqual("three", parser[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedAndNonFieldsTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				// one,"two",three
				writer.WriteLine("one,\"two\",three");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("one", parser[0]);
				Assert.AreEqual("two", parser[1]);
				Assert.AreEqual("three", parser[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithSpaceAfterTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				// one,"two" ,three
				writer.WriteLine("one,\"two\" ,three");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("one", parser[0]);
				Assert.AreEqual("two ", parser[1]);
				Assert.AreEqual("three", parser[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithSpaceBeforeTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				// one, "two",three
				writer.WriteLine("one, \"two\",three");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("one", parser[0]);
				Assert.AreEqual(" \"two\"", parser[1]);
				Assert.AreEqual("three", parser[2]);
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithQuoteAfterTest()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BadDataFound = null,
			};
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, config))
			{
				// 1,"two" "2,3
				writer.WriteLine("1,\"two\" \"2,3");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("two \"2", parser[1]);
				Assert.AreEqual("3", parser[2]);

				Assert.IsFalse(parser.Read());
			}
		}

		[TestMethod]
		public void ParseEscapedFieldWithEscapedQuoteTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				// 1,"two "" 2",3
				writer.WriteLine("1,\"two \"\" 2\",3");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual(3, parser.Count);
				Assert.AreEqual("1", parser[0]);
				Assert.AreEqual("two \" 2", parser[1]);
				Assert.AreEqual("3", parser[2]);
			}
		}

		[TestMethod]
		public void ParseFieldMissingQuoteGoesToEndOfFileTest()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			using (var reader = new StreamReader(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.WriteLine("a,b,\"c");
				writer.WriteLine("d,e,f");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("a", parser[0]);
				Assert.AreEqual("b", parser[1]);
				Assert.AreEqual("c\r\nd,e,f\r\n", parser[2]);
			}
		}

		private class Simple
		{
			public int Id { get; set; }

			public string Name { get; set; }
		}
	}
}
