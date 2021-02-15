using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace REPL.Tests
{
	public class UnitTest1
	{
		[Theory]
		//[InlineData("Int32", "1")]
		[InlineData("Single", "1.0")]
		//[InlineData("String", "abc")]
		public void Binding_StringArguments(string result, string arguments)
		{
			var type = typeof(SomeClass);
			var methods = type.GetMethods().ToArray().Where(o => o.Name == nameof(SomeClass.SomeMethod2));
			var bound = Binding.BindMethod(methods, arguments.Split(' '));

			var parameters = string.Join(",", bound.GetParameters().Select(p => p.ParameterType.Name));
			parameters.Should().Be(result);
		}

		[Theory]
		[InlineData("Int32", 1)]
		[InlineData("Single", 1f)]
		[InlineData("String", "")]
		[InlineData("Object", 1D)]
		//Doesn't work [InlineData("Object", 1D, 1f)]
		public void Binding_TypedArguments(string result, params object[] args)
		{
			var type = typeof(SomeClass);
			var methods = type.GetMethods().ToArray().Where(o => o.Name == nameof(SomeClass.SomeMethod));
			var bound = Binding.BindMethod(methods, args);
			bound.Should().NotBeNull();
			var parameters = string.Join(",", bound.GetParameters().Select(p => p.ParameterType.Name));
			parameters.Should().Be(result);
		}

		class SomeClass
		{
			public async Task<string> SomeMethodAsync(float fract) => "";

			public string SomeMethod(params object[] obj) => "";
			public string SomeMethod(object obj) => "";
			public string SomeMethod(string str) => "";
			public string SomeMethod(int num) => "";

			public string SomeMethod2(params object[] obj) => "";
			public string SomeMethod2(object obj) => "";
			public string SomeMethod2(int num) => "";
			public string SomeMethod2(float num) => "";
		}
	}
}
