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
    public class RefillingTextReaderTests
    {
		[TestMethod]
        public void RefillTextReaderMultipleTimesTest()
		{
			using (var stream = new MemoryStream())
			using (var reader = new StreamReader(stream))
			using (var writer = new StreamWriter(stream))
			using (var parser = new CsvParser(reader, CultureInfo.InvariantCulture))
			{
				writer.Write("1,2\r\n");
				writer.Flush();
				stream.Position = 0;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("1", parser[0].ToString());
				Assert.AreEqual("2", parser[1].ToString());
				Assert.IsFalse(parser.Read());

				var position = stream.Position;
				writer.Write("3,4\r\n");
				writer.Flush();
				stream.Position = position;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("3", parser[0].ToString());
				Assert.AreEqual("4", parser[1].ToString());
				Assert.IsFalse(parser.Read());

				position = stream.Position;
				writer.Write("5,6\r\n");
				writer.Flush();
				stream.Position = position;

				Assert.IsTrue(parser.Read());
				Assert.AreEqual("5", parser[0].ToString());
				Assert.AreEqual("6", parser[1].ToString());
				Assert.IsFalse(parser.Read());
			}
		}
    }
}
