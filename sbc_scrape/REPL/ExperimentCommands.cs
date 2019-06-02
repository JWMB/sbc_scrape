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
		public override async Task<object> Evaluate(List<object> parms) => string.Join("", string.Join(' ', parms).Reverse());
	}
	class HelloCmd : Command
	{
		public override string Id => "hello";
		public override async Task<object> Evaluate(List<object> parms) => $"Hello {parms.FirstOrDefault() ?? "Unknown"}!";
	}

	class ObjectCmd : Command
	{
		public override string Id => "object";
		public override async Task<object> Evaluate(List<object> parms) => new MediusFlowAPI.InvoiceFull.FilenameFormat {
			Id = 1, InvoiceDate = DateTime.Today.AddDays(-10), RegisteredDate = DateTime.Today, State = 1, Supplier = "Jonte AB" };
	}
	class AddCommandsTestCmd : Command, IUpdateCommandList
	{
		public AddCommandsTestCmd() { }
		public override string Id => "addtest";
		public override async Task<object> Evaluate(List<object> parms)
		{
			return "Will add commands";
		}

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
