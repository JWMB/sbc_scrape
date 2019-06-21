using System;
using System.Collections.Generic;
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

		public virtual string Help { get; }

		public ConsoleBase Console { get; set; } //TODO: prolly better as an argument to Evaluate

		public static bool TryParseArgument<T>(List<object> parms, int index, out T value)
		{
			object parsed = null;
			if (parms != null && parms.Count > index)
			{
				Func<object, object> convert = null;
				if (typeof(T) == typeof(long))
				{
					convert = o => Convert.ToInt64(o);
					//if (long.TryParse(val, out var lr)
					//	parsed = Convert.ChangeType(lr, typeof(T));
				}
				else if (typeof(T) == typeof(DateTime))
				{
					convert = o => Convert.ToDateTime(o);
					//if (DateTime.TryParse(val, out var p))
					//	parsed = Convert.ChangeType(p, typeof(T));
				}
				else if (typeof(T) == typeof(string))
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
				value = default(T);
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
	}
}
