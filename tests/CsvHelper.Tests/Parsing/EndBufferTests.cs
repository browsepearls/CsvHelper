using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Parsing
{
	[TestClass]
    public class EndBufferTests
    {
		[TestMethod]
		public void Read_BufferEndsInOneCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmno,pqrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual(2, parser.Count);
				Assert.AreEqual("abcdefghijklmno", parser[0]);
				Assert.AreEqual("pqrs", parser[1]);
			}
		}

		[TestMethod]
		public void Read_BufferEndsInFirstCharOfTwoCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmnop;;qrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16,
				Delimiter = ";;",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual(2, parser.Count);
				Assert.AreEqual("abcdefghijklmnop", parser[0]);
				Assert.AreEqual("qrs", parser[1]);
			}
		}

		[TestMethod]
		public void Read_BufferEndsInSecondCharOfTwoCharDelimiter_ParsesFieldCorrectly()
		{
			var s = new StringBuilder();
			s.Append("abcdefghijklmno;;pqrs\r\n");
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				BufferSize = 16,
				Delimiter = ";;",
			};
			using (var reader = new StringReader(s.ToString()))
			using (var parser = new CsvParser(reader, config))
			{
				Assert.IsTrue(parser.Read());
				Assert.AreEqual(2, parser.Count);
				Assert.AreEqual("abcdefghijklmno", parser[0]);
				Assert.AreEqual("pqrs", parser[1]);
			}
		}
	}
}
