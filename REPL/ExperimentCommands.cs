﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace REPL
{
	public class ReverseCmd : Command
	{
		public override string Id => "rev";
		public string Evaluate(IEnumerable<object> parms) => string.Join("", string.Join(" ", parms).Reverse());
	}
	public class HelloCmd : Command
	{
		public override string Id => "hello";
		public string Evaluate(object? parmameter) => $"Hello {parmameter ?? "Unknown"}!";
	}

	public class ObjectCmd : Command
	{
		public override string Id => "object";
		public object Evaluate() => new {
			Id = 1, InvoiceDate = DateTime.Today.AddDays(-10), RegisteredDate = DateTime.Today, State = 1, Supplier = "Jonte AB"
		};
	}
	public class AddCommandsTestCmd : Command, IUpdateCommandList
	{
		public AddCommandsTestCmd() { }
		public override string Id => "addtest";
		public string Evaluate() => "Will add commands";

		public IEnumerable<Command> UpdateCommandList(IEnumerable<Command> currentCommandList)
		{
			currentCommandList = currentCommandList.Where(c => !(c is ListCmd))
				.Concat(new Command[] {
						new HelloCmd(),
						new ReverseCmd(),
						new ObjectCmd(),
					});
			return currentCommandList.Concat(new Command[] { new ListCmd(currentCommandList) });
		}
	}
}
