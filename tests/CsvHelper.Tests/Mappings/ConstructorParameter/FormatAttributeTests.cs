using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	[TestClass]
	public class FormatAttributeTests
    {
		private const string FORMAT = "MM|dd|yyyy";
		private const string DATE = "12|25|2020";
		private readonly DateTimeOffset date = DateTimeOffset.ParseExact(DATE, FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None);

		[TestMethod]
		public void AutoMap_WithCultureInfoAttributes_ConfiguresParameterMaps()
		{
			var context = new CsvContext(new CsvConfiguration(CultureInfo.InvariantCulture));
			var map = context.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsNull(map.ParameterMaps[0].Data.TypeConverterOptions.Formats);
			Assert.AreEqual(1, map.ParameterMaps[1].Data.TypeConverterOptions.Formats.Count());
			Assert.AreEqual(FORMAT, map.ParameterMaps[1].Data.TypeConverterOptions.Formats[0]);
		}

		[TestMethod]
		public void GetRecords_WithCultureInfoAttributes_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "date" },
				{ "1", DATE },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(date, records[0].Date);
			}
		}

		[TestMethod]
		public void GetRecords_WithCultureInfoAttributes_NoHeader_CreatesRecords()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", DATE },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(date, records[0].Date);
			}
		}

		[TestMethod]
		public void WriteRecords_WithCultureInfoAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, date),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Date", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual(date.ToString(null, CultureInfo.InvariantCulture), serializer.Records[1][1]);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public DateTimeOffset Date { get; private set; }

			public Foo(int id, [Format(FORMAT)] DateTimeOffset date)
			{
				Id = id;
				Date = date;
			}
		}
	}
}
