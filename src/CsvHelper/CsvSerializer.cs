// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.IO;
using CsvHelper.Configuration;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to serialize data into a CSV file.
	/// </summary>
	public class CsvSerializer : ISerializer
	{
		private readonly TextWriter writer;
		private readonly WritingContext context;
		private readonly string delimiter;
		private readonly bool leaveOpen;
		private readonly NewLine newLine;
		private readonly string newLineString;
		private readonly char[] injectionCharacters;
		private readonly char injectionEscapeCharacter;
		private readonly char quote;
		private readonly bool sanitizeForInjection;

		private int row = 1;
		private bool disposed;

		/// <summary>
		/// Gets the current row that's being written to.
		/// </summary>
		public virtual int Row => row;

		/// <summary>
		/// Gets the writing context.
		/// </summary>
		public virtual WritingContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual ISerializerConfiguration Configuration { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvSerializer"/> class.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV data to.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen"><c>true</c> to leave the <see cref="TextWriter"/> open after the <see cref="CsvSerializer"/> object is disposed, otherwise <c>false</c>.</param>
		public CsvSerializer(TextWriter writer, CultureInfo culture, bool leaveOpen = false) : this(writer, new CsvConfiguration(culture) { LeaveOpen = leaveOpen }) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvSerializer"/> class.
		/// </summary>
		/// <param name="writer">The writer.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvSerializer(TextWriter writer, CsvConfiguration configuration)
		{
			this.writer = writer;

			delimiter = configuration.Delimiter;
			context = new WritingContext(this);
			injectionCharacters = configuration.InjectionCharacters;
			injectionEscapeCharacter = configuration.InjectionEscapeCharacter;
			leaveOpen = configuration.LeaveOpen;
			newLine = configuration.NewLine;
			newLineString = configuration.NewLineString;
			quote = configuration.Quote;
			sanitizeForInjection = configuration.SanitizeForInjection;

			Configuration = configuration;
		}

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		public virtual void Write(string[] record)
		{
			// Don't forget about the async method below!

			for (var i = 0; i < record.Length; i++)
			{
				if (i > 0)
				{
					writer.Write(delimiter);
				}

				var field = sanitizeForInjection
					? SanitizeForInjection(record[i])
					: record[i];

				writer.Write(field);
			}
		}

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		public virtual async Task WriteAsync(string[] record)
		{
			for (var i = 0; i < record.Length; i++)
			{
				if (i > 0)
				{
					await writer.WriteAsync(delimiter).ConfigureAwait(false);
				}

				var field = sanitizeForInjection
					? SanitizeForInjection(record[i])
					: record[i];

				await writer.WriteAsync(field).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Writes a new line to the CSV file.
		/// </summary>
		public virtual void WriteLine()
		{
			// Don't forget about the async method below!

			writer.Write(newLineString);
			row++;
		}

		/// <summary>
		/// Writes a new line to the CSV file.
		/// </summary>
		public virtual async Task WriteLineAsync()
		{
			await writer.WriteAsync(newLineString).ConfigureAwait(false);
		}

		/// <summary>
		/// Sanitizes the field to prevent injection.
		/// </summary>
		/// <param name="field">The field to sanitize.</param>
		protected virtual string SanitizeForInjection(string field)
		{
			if (string.IsNullOrEmpty(field))
			{
				return field;
			}

			if (ArrayHelper.Contains(injectionCharacters, field[0]))
			{
				return injectionEscapeCharacter + field;
			}

			if (field[0] == quote && ArrayHelper.Contains(injectionCharacters, field[1]))
			{
				return field[0].ToString() + injectionEscapeCharacter.ToString() + field.Substring(1);
			}

			return field;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public virtual void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				// Dispose managed state (managed objects)
				if (!leaveOpen)
				{
					writer?.Dispose();
				}
			}

			disposed = true;
		}

#if !NET45
		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual async ValueTask DisposeAsync()
		{
			await DisposeAsync(true).ConfigureAwait(false);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual async ValueTask DisposeAsync(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				await writer.DisposeAsync().ConfigureAwait(false);
			}

			disposed = true;
		}
#endif
	}
}
