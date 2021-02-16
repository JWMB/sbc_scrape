using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace REPL
{
	public interface IQuitCommand
	{ }
	public interface IUpdateCommandList
	{
		IEnumerable<Command> UpdateCommandList(IEnumerable<Command> currentCommandList);
	}

	public abstract class Command
	{
		public abstract string Id { get; }
		public abstract Task<object> Evaluate(List<object> parms);

		public virtual string Help { get; } = string.Empty;

		public ConsoleBase? Console { get; set; } //TODO: prolly better as an argument to Evaluate

		public static bool TryParseArgument<T>(List<object> parms, int index, [MaybeNullWhen(false)] out T value)
		{
			object? parsed = null;
			if (parms != null && parms.Count > index)
			{
				var type = typeof(T);
				type = Nullable.GetUnderlyingType(type) ?? type;

				Func<object, object>? convert = null;
				if (type == typeof(long))
					convert = o => Convert.ToInt64(o);
				else if (type == typeof(int))
					convert = o => Convert.ToInt32(o);
				else if (type == typeof(DateTime))
					convert = o => Convert.ToDateTime(o);
				else if (type == typeof(string))
					convert = o => o.ToString();
				if (convert != null)
				{
					try
					{
						parsed = convert(parms[index]);
					}
					catch { }
				}
			}
			if (parsed == null)
			{
				value = default;
				return false;
			}
			value = (T)parsed;
			return true;
		}
		public static T ParseArgument<T>(List<object> parms, int index, T defaultValue)
		{
			if (TryParseArgument<T>(parms, index, out var parsed))
				return parsed;
			return defaultValue;
		}

		public static List<T> ParseArguments<T>(List<object> parms, T defaultValue)
			=> parms.Select((o, i) => ParseArgument(parms, i, defaultValue)).ToList();
	}
}
