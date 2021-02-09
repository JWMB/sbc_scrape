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
			//same number of parameters
			////most narrow type match
			var inputList = inputParameters.ToList();
			//var inputTypes = input.Select(o => o == null ? null : o.GetType());
			var parsedInputs = new List<object>();
			var matched = methods.Select(method =>
			{
				var parms = method.GetParameters();
				if (inputList.Count > parms.Count())
					return 9;
				for (int i = 0; i < inputList.Count; i++)
				{
					var inputType = inputList[i]?.GetType();
					if (inputType == null)
					{
						if (Nullable.GetUnderlyingType(parms[i].ParameterType) == null)
							return 99;
					}
					else
					{
						if (parms[i].ParameterType.IsAssignableFrom(inputType) == false)
						{
							if (inputType == typeof(string))
							{
								//
							}
						}
						return 99;
					}
				}
				return 0;
			}).ToList();

			return Type.DefaultBinder.SelectMethod(BindingFlags.Default, methods.ToArray(), inputParameters.Select(o => o == null ? null : o.GetType()).ToArray(), null);
		}
	}
}
