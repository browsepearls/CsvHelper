using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Helper methods for arrays.
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
		public static ReadOnlySpan<char> Trim(ReadOnlySpan<char> span, params char[] trimChars)
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

		/// <summary>
		/// Determines whether this given array contains the given character.
		/// </summary>
		/// <param name="span">The array to search.</param>
		/// <param name="c">The character to look for.</param>
		/// <returns>
		///   <c>true</c> if the array contains the characters, otherwise <c>false</c>.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains(ReadOnlySpan<char> span, char c)
		{
			for (var i = 0; i < span.Length; i++)
			{
				if (span[i] == c)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Compares a <c>string</c> and a <c>Span{char}</c> for equality.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="span">The span.</param>
		/// <returns><c>true</c> if they are equal, otherwise <c>false</c>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Equals(string text, ReadOnlySpan<char> span)
		{
			if (text.Length != span.Length)
			{
				return false;
			}

			var textChars = text.ToCharArray();

			for (var i = 0; i < textChars.Length; i++)
			{
				if (textChars[i] != span[i])
				{
					return false;
				}
			}

			return true;
		}
	}
}
