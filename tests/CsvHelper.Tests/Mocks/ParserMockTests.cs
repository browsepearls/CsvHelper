using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mocks
{
	[TestClass]
    public class ParserMockTests
    {
		[TestMethod]
        public void Test()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name" },
				{ "1", "one" },
			};
			Assert.IsTrue(parser.Read());
			Assert.AreEqual("Id", parser[0]);
			Assert.AreEqual("Name", parser[1]);

			Assert.IsTrue(parser.Read());
			Assert.AreEqual("1", parser[0]);
			Assert.AreEqual("one", parser[1]);

			Assert.IsFalse(parser.Read());
			Assert.IsFalse(parser.Read());
		}
	}
}
