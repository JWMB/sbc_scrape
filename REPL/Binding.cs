using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace REPL
{
	public static class Binding
	{
		class MethodMatchResult
		{
			public MethodBase Method { get; set; }
			public List<(int score, object? converted)> Matched { get; set; }
			public int TotalScore { get; set; }
			public bool HasMismatch { get; set; }
			public int NumOptionalArguments { get; set; }
		}

		private static MethodMatchResult MatchMethod(MethodBase method, IEnumerable<object> inputParameters, int skipNumOptionals = 0)
		{
			var parms = method.GetParameters();
			var requiredParms = new List<ParameterInfo>(parms);
			for (int i = 0; i < skipNumOptionals; i++)
			{
				var index = parms.Count() - i - 1;
				if (index < 0)
					break;
				if (requiredParms[index].IsOptional == false)
					break;
				requiredParms.RemoveAt(index);
			}

			var numMissingInputs = Math.Max(0, requiredParms.Count() - inputParameters.Count());
			var extendedInput = inputParameters.Concat(Enumerable.Range(0, numMissingInputs).Select(o => (object?)null));
			var pairs = extendedInput.Take(requiredParms.Count()).Select((o, i) => new { Input = o, Expected = requiredParms[i] });
			var matchedPairs = pairs.Select(pair => ScoreTypeMatch(pair.Input, pair.Expected.ParameterType)).ToList();
			for (int i = requiredParms.Count; i < parms.Count(); i++)
				matchedPairs.Add((0, Type.Missing)); // Needed for Invoke when optional args

			var numUnusedInputs = Math.Max(0, inputParameters.Count() - requiredParms.Count());
			return new MethodMatchResult
			{
				Method = method,
				Matched = matchedPairs,
				TotalScore = matchedPairs.Sum(o => o.score) - numUnusedInputs * 1,
				HasMismatch = matchedPairs.Any(o => o.score < 0),
				NumOptionalArguments = method.GetParameters().Count(o => o.IsOptional)
			};
		}

		public static (MethodBase method, object[] arguments) BindMethod(IEnumerable<MethodInfo> methods, IEnumerable<object> inputParameters)
		{
			// TODO: we could provide info about which inputs are always strings (e.g. from command line input)
			// Then we can score methods better

			var numOptionals = 0;

			// Inefficient - no need to re-evaluate methods that don't have any more optionals than already evaluated...
			while (true)
			{
				var matched = methods.Select(method => MatchMethod(method, inputParameters, numOptionals));

				var goodMatches = matched
					.Where(o => !o.HasMismatch)
					.OrderByDescending(o => o.TotalScore)
					.ToList();

				if (goodMatches.Any())
				{
					var found = goodMatches.First();
					return (found.Method, found.Matched.Select(o => o.converted).ToArray());
				}
				if (numOptionals < matched.Max(o => o.NumOptionalArguments))
					numOptionals++;
				else
					break;
			}

			//var inp = inputParameters.ToArray();
			//Type.DefaultBinder.BindToMethod(BindingFlags.Default, methods.ToArray(), ref inp,  System.Globalization.CultureInfo.InvariantCulture, )
			var method = Type.DefaultBinder.SelectMethod(BindingFlags.Default, methods.ToArray(), inputParameters.Select(o => o == null ? null : o.GetType()).ToArray(), null);
			return (method, inputParameters.ToArray());
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
