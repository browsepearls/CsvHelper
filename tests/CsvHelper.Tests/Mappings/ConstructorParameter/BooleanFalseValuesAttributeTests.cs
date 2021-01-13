using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{
	[TestClass]
	public class BooleanFalseValuesAttributeTests
	{
		[TestMethod]
		public void AutoMap_WithBooleanFalseValuesAttribute_CreatesParameterMaps()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			var map = context.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.AreEqual(0, map.ParameterMaps[0].Data.TypeConverterOptions.BooleanTrueValues.Count);
			Assert.AreEqual(0, map.ParameterMaps[0].Data.TypeConverterOptions.BooleanFalseValues.Count);
			Assert.AreEqual(0, map.ParameterMaps[1].Data.TypeConverterOptions.BooleanTrueValues.Count);
			Assert.AreEqual(1, map.ParameterMaps[1].Data.TypeConverterOptions.BooleanFalseValues.Count);
			Assert.AreEqual("Bar", map.ParameterMaps[1].Data.TypeConverterOptions.BooleanFalseValues[0]);
		}

		[TestMethod]
		public void GetRecords_WithBooleanFalseValuesAttribute_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "boolean" },
				{ "1", "Bar" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.IsFalse(records[0].Boolean);
			}
		}

		[TestMethod]
		public void GetRecords_WithBooleanFalseValuesAttribute_NoHeader_CreatesRecords()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "Bar" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.IsFalse(records[0].Boolean);
			}
		}

		[TestMethod]
		public void WriteRecords_WithBooleanFalseValuesAttribute_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, false),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Boolean", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual(false.ToString(), serializer.Records[1][1]);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public bool Boolean { get; private set; }

			public Foo(int id, [BooleanFalseValues("Bar")] bool boolean)
			{
				Id = id;
				Boolean = boolean;
			}
		}
	}
}
