using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// 
	/// </summary>
	public static class ArrayHelper
    {
		/// <summary>
		/// Trims the given characters off the start and end of the given span.
		/// </summary>
		/// <param name="span">The span.</param>
		/// <param name="trimChars">The characters to trim.</param>
		/// <returns>The trimmed span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<char> Trim(Span<char> span, params char[] trimChars)
		{
			var start = 0;
			var end = span.Length - 1;

			// Trim start.
			for (var i = start; i <= end; i++)
			{
				if (Contains(trimChars, span[i]))
				{
					start++;
				}
				else
				{
					break;
				}
			}

			// Trim end.
			for (var i = end; i >= start; i--)
			{
				if (Contains(trimChars, span[i]))
				{
					end--;
				}
				else
				{
					break;
				}
			}

			return span.Slice(start, end - start + 1);
		}

		/// <summary>
		/// Determines whether this given array contains the given character.
		/// </summary>
		/// <param name="array">The array to search.</param>
		/// <param name="c">The character to look for.</param>
		/// <returns>
		///   <c>true</c> if the array contains the characters, otherwise <c>false</c>.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains(char[] array, char c)
		{
			for (var i = 0; i < array.Length; i++)
			{
				if (array[i] == c)
				{
					return true;
				}
			}

			return false;
		}
	}
}
