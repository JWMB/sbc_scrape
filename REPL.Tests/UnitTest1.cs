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
		[Fact]
		public async Task REPL_Test()
		{
			var cmds = new List<Command> {
					new QuitCmd(),
					new CSVCmd(),
					new AddCommandsTestCmd(),
					};
			cmds.Add(new ListCmd(cmds));

			var queue = new Queue<(string, string)>(new[] {
				("l", nameof(ListCmd)),
				("q", nameof(QuitCmd))
			});
			(string, string) currentItem = ("", "");

			var REPLRunner = new Runner(cmds);
			REPLRunner.CallAndResult += REPLRunner_CallAndResult;

			var reader = new MockInput();
			reader.OnRead = () => {
				currentItem = queue.Any() ? queue.Dequeue() : ("q", "");
				reader.Write(currentItem.Item1 + "\n");
			};
			await REPLRunner.RunREPL(reader, System.Threading.CancellationToken.None);

			void REPLRunner_CallAndResult(object sender, Runner.CallAndResultEventArgs e)
			{
				if (e.Method.DeclaringType.Name != currentItem.Item2)
				{

				}
			}
		}

		class MockInput : IInputReader
		{
			private string buffer = "";
			public Action? OnRead = null;
			public void Write(string str)
			{
				buffer += str;
			}
			public string Read()
			{
				OnRead?.Invoke();
				var result = buffer;
				buffer = "";
				return result;
			}
		}

		[Theory]
		//[InlineData("Int32", "1")]
		[InlineData("Single", "1.0")]
		//[InlineData("String", "abc")]
		public void Binding_StringArguments(string result, string arguments)
		{
			var type = typeof(SomeClass);
			var methods = type.GetMethods().ToArray().Where(o => o.Name == nameof(SomeClass.SomeMethod2));
			var (method, args) = Binding.BindMethod(methods, arguments.Split(' '));

			var parameters = string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name));
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
			var (method, castArgs) = Binding.BindMethod(methods, args);
			method.Should().NotBeNull();
			var parameters = string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name));
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
