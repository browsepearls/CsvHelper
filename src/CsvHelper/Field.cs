using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// A field that is made up of many segements.
	/// </summary>
	[DebuggerTypeProxy(typeof(FieldPositionDebugView))]
	[DebuggerDisplay(@"Count = {Count}, Current = \{{Current}\}")]
	public class Field : IEnumerable<FieldPosition>
	{
		private FieldPosition[] positions;

		/// <summary>
		/// Gets the number of field position parts.
		/// </summary>
		public int Count { get; private set; }

		/// <summary>
		/// Gets the current field position part.
		/// </summary>
		public FieldPosition Current => positions[Count - 1];

		/// <summary>
		/// Gets the <see cref="FieldPosition"/> at the specified index.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <exception cref="IndexOutOfRangeException"></exception>
		public FieldPosition this[int index]
		{
			get
			{
				if (index >= Count)
				{
					throw new IndexOutOfRangeException();
				}

				return positions[index];
			}
		}

		/// <summary>
		/// Gets the sum of the lengths of the positions.
		/// </summary>
		public int Length
		{
			get
			{
				var sum = 0;
				for (var i = 0; i < Count; i++)
				{
					sum += positions[i].Length;
				}

				return sum;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Field"/> class.
		/// </summary>
		/// <param name="capacity">The initial capacity.</param>
		public Field(int capacity = 128)
		{
			positions = new FieldPosition[capacity];
			FillPositions();
		}

		/// <summary>
		/// Adds a new field position part.
		/// </summary>
		public void Add()
		{
			if (Count >= positions.Length)
			{
				var temp = new FieldPosition[positions.Length * 2];
				Array.Copy(positions, temp, positions.Length);
				positions = temp;
				FillPositions();
			}

			Count++;

			positions[Count - 1].Clear();
		}

		/// <summary>
		/// Clears all field position parts.
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
		public IEnumerator<FieldPosition> GetEnumerator()
		{
			for (var i = 0; i < Count; i++)
			{
				yield return positions[i];
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
			for (var i = Count; i < positions.Length; i++)
			{
				positions[i] = new FieldPosition();
			}
		}

		private class FieldPositionDebugView
		{
			private Field fieldPosition;

			public int Count => fieldPosition.Count;

			public FieldPosition Current => fieldPosition.Current;

			public FieldPosition[] Parts => fieldPosition.positions.Take(fieldPosition.Count).ToArray();

			public FieldPositionDebugView(Field fieldPosition)
			{
				this.fieldPosition = fieldPosition;
			}
		}
	}
}
