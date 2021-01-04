﻿using CsvHelper.Configuration;
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
    public class NameIndexAttributeTests
    {
		[TestMethod]
		public void AutoMap_WithNameAttributes_ConfiguresParameterMaps()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var map = config.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.AreEqual(0, map.ParameterMaps[0].Data.NameIndex);
			Assert.AreEqual(1, map.ParameterMaps[1].Data.NameIndex);
		}

		[TestMethod]
		public void GetRecords_WithNameAttributes_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "Id", "Name", "Name" },
				{ "1", "", "one" },
				null
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("one", records[0].Name);
			}
		}

		[TestMethod]
		public void GetRecords_WithNameAttributes_NoHeader_CreatesRecordsUsingIndexes()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = false,
			};
			var parser = new ParserMock(config)
			{
				{ "1", "one" },
				null
			};
			using (var csv = new CsvReader(parser))
			{
				// Can't read using names when no header is present.
				Assert.ThrowsException<ReaderException>(() => csv.GetRecords<Foo>().ToList());
			}
		}

		[TestMethod]
		public void WriteRecords_WithNameAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, "one"),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Name", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual("one", serializer.Records[1][1]);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public Foo([Name("Id")]int id, [Name("Name")][NameIndex(1)] string name)
			{
				Id = id;
				Name = name;
			}
		}
	}
}
