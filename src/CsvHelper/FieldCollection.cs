using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Collection that holds <see cref="Field"/>s.
	/// </summary>
	[DebuggerDisplay("Count = {Count}")]
	[DebuggerTypeProxy(typeof(FieldPositionsDebugView))]
	public class FieldCollection : IEnumerable<Field>
	{
		private Field[] fields;

		/// <summary>
		/// Gets the number of field positions.
		/// </summary>
		public int Count { get; private set; }

		/// <summary>
		/// Gets the current field position.
		/// </summary>
		public Field Current => fields[Count - 1];

		/// <summary>
		/// Gets the <see cref="FieldPosition"/> at the specified index.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <exception cref="IndexOutOfRangeException"></exception>
		public Field this[int index]
		{
			get
			{
				if (index >= Count)
				{
					throw new IndexOutOfRangeException();
				}

				return fields[index];
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FieldCollection"/> class.
		/// </summary>
		/// <param name="capacity">The initial capacity.</param>
		public FieldCollection(int capacity = 128)
		{
			fields = new Field[capacity];
			FillPositions();
		}

		/// <summary>
		/// Adds a new field position.
		/// </summary>
		public void Add()
		{
			if (Count >= fields.Length)
			{
				var temp = new Field[fields.Length * 2];
				Array.Copy(fields, temp, fields.Length);
				fields = temp;
				FillPositions();
			}

			Count++;

			fields[Count - 1].Clear();
		}

		/// <summary>
		/// Clears all field positions.
		/// </summary>
		public void Clear()
		{
			Count = 0;
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// An enumerator that can be used to iterate through the collection.
		/// </returns>
		public IEnumerator<Field> GetEnumerator()
		{
			for (var i = 0; i < Count; i++)
			{
				yield return fields[i];
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
		/// </returns>
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private void FillPositions()
		{
			for (var i = Count; i < fields.Length; i++)
			{
				fields[i] = new Field();
			}
		}

		private class FieldPositionsDebugView
		{
			private readonly FieldCollection fieldPositions;

			public int Count => fieldPositions.Count;

			public Field Current => fieldPositions.Current;

			public Field[] Positions => fieldPositions.fields.Take(fieldPositions.Count).ToArray();

			public FieldPositionsDebugView(FieldCollection fieldPositions)
			{
				this.fieldPositions = fieldPositions;
			}
		}
	}
}
