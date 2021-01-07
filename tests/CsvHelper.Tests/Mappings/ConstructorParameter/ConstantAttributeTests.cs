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
    public class ConstantAttributeTests
    {
		[TestMethod]
		public void AutoMap_WithConstantAttributes_ConfiguresParameterMaps()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var context = new CsvContext(config);
			var map = context.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsFalse(map.ParameterMaps[0].Data.IsConstantSet);
			Assert.IsNull(map.ParameterMaps[0].Data.Constant);
			Assert.IsTrue(map.ParameterMaps[1].Data.IsConstantSet);
			Assert.AreEqual("Bar", map.ParameterMaps[1].Data.Constant);
		}

		[TestMethod]
		public void GetRecords_WithConstantAttributes_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "name" },
				{ "1", "one" },
				null
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("Bar", records[0].Name);
			}
		}

		[TestMethod]
		public void GetRecords_WithConstantAttributes_NoHeader_CreatesRecords()
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
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual("Bar", records[0].Name);
			}
		}

		[TestMethod]
		public void WriteRecords_WithConstantAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, null),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Name", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual("", serializer.Records[1][1]);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public string Name { get; private set; }

			public Foo(int id, [Constant("Bar")] string name)
			{
				Id = id;
				Name = name;
			}
		}
	}
}
