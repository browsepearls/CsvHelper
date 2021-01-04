// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using CsvHelper.Tests.Mocks;

namespace CsvHelper.Tests.TypeConversion
{
	[TestClass]
	public class EnumerableConverterTests
	{
		[TestMethod]
		public void ConvertTest()
		{
			var converter = new EnumerableConverter();

			var propertyMapData = new MemberMapData(null);
			propertyMapData.TypeConverterOptions.CultureInfo = CultureInfo.CurrentCulture;

			var readerRow = new CsvReader(new ParserMock());
			var writerRow = new CsvWriter(new SerializerMock());

			Assert.ThrowsException<TypeConverterException>(() => converter.ConvertFromString("", readerRow, propertyMapData));
			Assert.ThrowsException<TypeConverterException>(() => converter.ConvertToString(5, writerRow, propertyMapData));
		}
	}
}
