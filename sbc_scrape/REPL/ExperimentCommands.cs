using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.REPL
{
	class ReverseCmd : Command
	{
		public override string Id => "rev";
		public override async Task<string> Execute(List<string> parms) => string.Join("", string.Join(' ', parms).Reverse());
	}
	class HelloCmd : Command
	{
		public override string Id => "hello";
		public override async Task<string> Execute(List<string> parms) => $"Hello {parms.FirstOrDefault() ?? "Unknown"}!";
	}

	class AddCommandsTestCmd : Command, IUpdateCommandList
	{
		public AddCommandsTestCmd() { }
		public override string Id => "addtest";
		public override async Task<string> Execute(List<string> parms)
		{
			return "Will add commands";
		}

		public IEnumerable<Command> UpdateCommandList(IEnumerable<Command> currentCommandList)
		{
			currentCommandList = currentCommandList.Where(c => !(c is ListCmd))
				.Concat(new Command[] {
						new HelloCmd(),
						new ReverseCmd(),
					});
			return currentCommandList.Concat(new Command[] { new ListCmd(currentCommandList) });
		}

	}
}
