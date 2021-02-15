using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace REPL
{
	public static class Binding
	{
		public static MethodBase BindMethod(IEnumerable<MethodInfo> methods, IEnumerable<object> inputParameters)
		{
			// TODO: we could provide info about which inputs are always strings (e.g. from command line input)
			// Then we can score methods better
			var matched = methods.Select(method =>
			{
				var parms = method.GetParameters();

				var numMissingInputs = Math.Max(0, parms.Count() - inputParameters.Count());
				var extendedInput = inputParameters.Concat(Enumerable.Range(0, numMissingInputs).Select(o => (object?)null));
				var pairs = extendedInput.Take(parms.Count()).Select((o, i) => new { Input = o, Expected = parms[i] });
				var matchedPairs = pairs.Select(pair => ScoreTypeMatch(pair.Input, pair.Expected.ParameterType)).ToList();

				var numUnusedInputs = Math.Max(0, inputParameters.Count() - parms.Count());
				return new
				{
					Method = method,
					Matched = matchedPairs,
					TotalScore = matchedPairs.Sum(o => o.score) - numUnusedInputs * 1,
					HasMismatch = matchedPairs.Any(o => o.score < 0)
				};
			})
				.Where(o => !o.HasMismatch)
				.OrderByDescending(o => o.TotalScore)
				.ToList();

			var found = matched.FirstOrDefault();
			if (found != null)
				return found.Method;

			return Type.DefaultBinder.SelectMethod(BindingFlags.Default, methods.ToArray(), inputParameters.Select(o => o == null ? null : o.GetType()).ToArray(), null);
		}

		public static (int score, object? converted) ScoreTypeMatch(object? input, Type parameterType) //ParameterInfo expected)
		{
			//var parameterType = expected.ParameterType;
			if (input == null)
			{
				if (Nullable.GetUnderlyingType(parameterType) != null)
					return (0, null);
			}
			else
			{
				if (parameterType == input.GetType())
					return (3, input);
				if (parameterType.IsAssignableFrom(input.GetType()))
				{
					return (parameterType == typeof(object) ? 0 : 1, input);
				}
				else
				{
					try
					{
						var converted = Convert.ChangeType(input, parameterType, System.Globalization.CultureInfo.InvariantCulture);
						return (input.GetType() == typeof(string) ? 1 : 2, converted);
					}
					catch
					{
					}
				}
			}
			return (-1, null);
		}

		private static object ParseInput(string str)
		{
			if (double.TryParse(str, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("en-US"), out var @double))
			{
				if (int.TryParse(str, out var @int))
					return @int;
				return @double;
			}
			else if (bool.TryParse(str, out var @bool))
			{
				return @bool;
			}
			return str;
		}
	}
}
