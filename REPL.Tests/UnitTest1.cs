using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace REPL.Tests
{
	public class UnitTest1
	{
		[Theory]
		[InlineData("Int32", 1)]
		[InlineData("Single", 1f)]
		[InlineData("String", "")]
		[InlineData("Object", 1D)]
		//Doesn't work [InlineData("Object", 1D, 1f)]
		public void Test1(string result, params object[] args)
		{
			var type = typeof(SomeClass);
			var methods = type.GetMethods();
			var bound = Binding.BindMethod(methods, args);
			bound.Should().NotBeNull();
			var parameters = string.Join(",", bound.GetParameters().Select(p => p.ParameterType.Name));
			parameters.Should().Be(result);
		}

		class SomeClass
		{
			public async Task<string> SomeMethodAsync(float fract)
			{
				return string.Empty;
			}

			public string SomeMethod(params object[] obj)
			{
				return "";
			}

			public string SomeMethod(object obj)
			{
				return "";
			}
			public string SomeMethod(string str)
			{
				return "";
			}
			public string SomeMethod(int num)
			{
				return "";
			}
		}
	}
}
