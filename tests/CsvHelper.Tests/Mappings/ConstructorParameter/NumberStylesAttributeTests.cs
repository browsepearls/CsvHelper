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
	public class NumberStylesAttributeTests
    {
		private const decimal amount = 123;

		[TestMethod]
		public void AutoMap_WithCultureInfoAttributes_ConfiguresParameterMaps()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);
			var map = config.AutoMap<Foo>();

			Assert.AreEqual(2, map.ParameterMaps.Count);
			Assert.IsNull(map.ParameterMaps[0].Data.TypeConverterOptions.NumberStyles);
			Assert.AreEqual(NumberStyles.AllowParentheses, map.ParameterMaps[1].Data.TypeConverterOptions.NumberStyles);
		}

		[TestMethod]
		public void GetRecords_WithCultureInfoAttributes_HasHeader_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "amount" },
				{ "1", $"({amount})" },
				null
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(-amount, records[0].Amount);
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
				{ "1", $"({amount})" },
				null
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.AreEqual(1, records.Count);
				Assert.AreEqual(1, records[0].Id);
				Assert.AreEqual(-amount, records[0].Amount);
			}
		}

		[TestMethod]
		public void WriteRecords_WithCultureInfoAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, amount),
			};

			using (var serializer = new SerializerMock())
			using (var csv = new CsvWriter(serializer))
			{
				csv.WriteRecords(records);

				Assert.AreEqual(2, serializer.Records.Count);

				Assert.AreEqual("Id", serializer.Records[0][0]);
				Assert.AreEqual("Amount", serializer.Records[0][1]);

				Assert.AreEqual("1", serializer.Records[1][0]);
				Assert.AreEqual(amount.ToString(), serializer.Records[1][1]);
			}
		}

		private class Foo
		{
			public int Id { get; private set; }

			public decimal Amount { get; private set; }

			public Foo(int id, [NumberStyles(NumberStyles.AllowParentheses)] decimal amount)
			{
				Id = id;
				Amount = amount;
			}
		}
	}
}
