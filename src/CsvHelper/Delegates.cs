using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that gets called when bad data is found.
	/// </summary>
	/// <param name="context">The context.</param>
	public delegate void BadDataFound(CsvContext context);

	/// <summary>
	/// Function that chooses the constructor to use for constructor mapping.
	/// </summary>
	public delegate ConstructorInfo GetConstructor(Type classType);

	/// <summary>
	/// Function that gets the name to use for the property of the dynamic object.
	/// </summary>
	public delegate string GetDynamicPropertyName(CsvContext context, int fieldIndex);

	/// <summary>
	/// Function that is called when a header validation check is ran. The default function
	/// will throw a <see cref="ValidationException"/> if there is no header for a given member mapping.
	/// You can supply your own function to do other things like logging the issue instead of throwing an exception.
	/// </summary>
	public delegate void HeaderValidated(InvalidHeader[] invalidHeaders, CsvContext context);

	/// <summary>
	/// Function that is called when a missing field is found. The default function will
	/// throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things
	/// like logging the issue instead of throwing an exception.
	/// </summary>
	public delegate void MissingFieldFound(string[] headerNames, int index, CsvContext context);

	/// <summary>
	/// Function that prepares the header field for matching against a member name.
	/// The header field and the member name are both ran through this function.
	/// You should do things like trimming, removing whitespace, removing underscores,
	/// and making casing changes to ignore case.
	/// </summary>
	public delegate string PrepareHeaderForMatch(string header, int fieldIndex);

	/// <summary>
	/// Function that is called when a reading exception occurs.
	/// The default function will re-throw the given exception. If you want to ignore
	/// reading exceptions, you can supply your own function to do other things like
	/// logging the issue.
	/// </summary>
	public delegate bool ReadingExceptionOccurred(CsvHelperException exception);

	/// <summary>
	/// Function that will return the prefix for a reference header.
	/// </summary>
	public delegate string ReferenceHeaderPrefix(Type memberType, string memberName);

	/// <summary>
	/// Function that is used to determine if a field should get quoted when writing.
	/// </summary>
	public delegate bool ShouldQuote(string field, CsvContext context);

	/// <summary>
	/// Function that determines whether to skip the given record or not.
	/// </summary>
	public delegate bool ShouldSkipRecord(string[] record);

	/// <summary>
	/// Function that determines if constructor parameters should be used to create
	/// the class instead of the default constructor and members.
	/// </summary>
	public delegate bool ShouldUseConstructorParameters(Type parameterType);
}
