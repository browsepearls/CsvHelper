using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Manages member change event subscriptions for a class.
	/// </summary>
	/// <typeparam name="TClass">The type of the class.</typeparam>
	public class PropertyChangedEvents<TClass>
	{
		private Dictionary<string, List<Delegate>> handlers = new Dictionary<string, List<Delegate>>();

		/// <summary>
		/// Adds the changed callback handler for the given member.
		/// </summary>
		/// <typeparam name="TMember">The type of the member.</typeparam>
		/// <param name="expression">The member expression.</param>
		/// <param name="action">The callback handler.</param>
		public virtual void AddChangedHandler<TMember>(Expression<Func<TClass, TMember>> expression, Action<TMember> action)
		{
			var memberName = ReflectionHelper.GetMember(expression).Name;
			if (!handlers.ContainsKey(memberName))
			{
				handlers[memberName] = new List<Delegate>();
			}

			handlers[memberName].Add(action);
		}

		/// <summary>
		/// Removes the changed callback handler for the given member.
		/// </summary>
		/// <typeparam name="TMember">The type of the member.</typeparam>
		/// <param name="expression">The member expression.</param>
		/// <param name="action">The callback handler.</param>
		public virtual void RemoveChangedHandler<TMember>(Expression<Func<TClass, TMember>> expression, Action<TMember> action = null)
		{
			var memberName = ReflectionHelper.GetMember(expression).Name;
			if (!handlers.ContainsKey(memberName))
			{
				return;
			}

			if (action == null)
			{
				handlers[memberName].Clear();
			}
			else
			{
				handlers[memberName].Remove(action);
			}
		}

		/// <summary>
		/// Removes all registered change events for all members.
		/// </summary>
		public virtual void Clear()
		{
			handlers.Clear();
		}

		/// <summary>
		/// Called when a property has changed.
		/// </summary>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="value">The value.</param>
		/// <param name="memberName">Name of the member.</param>
		public virtual void OnChanged<TValue>(TValue value, [CallerMemberName] string memberName = null)
		{
			if (!handlers.ContainsKey(memberName))
			{
				return;
			}

			var actions = handlers[memberName];
			foreach (var action in actions)
			{
				action.DynamicInvoke(value);
			}
		}
	}
}
