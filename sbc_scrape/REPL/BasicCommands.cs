using MediusFlowAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.REPL
{

	class NullCmd : Command
	{
		public override string Id => "";
		public override async Task<string> Execute(List<string> parms)
		{
			return parms.Count == 0 ? "" : $"{parms.First()} not found";
		}
	}

	class QuitCmd : Command, IQuitCommand
	{
		public override string Id => "q";
		public override async Task<string> Execute(List<string> parms)
		{
			return "Goodbye!";
		}
	}
	class ListCmd : Command
	{
		private readonly IEnumerable<Command> commands;
		public override string Id => "l";
		public ListCmd(IEnumerable<Command> commands) => this.commands = commands;
		public override async Task<string> Execute(List<string> parms)
		{
			return "Available commands:\n"
				+ string.Join("\n",
				commands.Select(c => $"{c.GetType().Name.Replace("Cmd", "")}: {c.Id}\n{c.Help}"));
		}
	}

	class WriteFileCmd : Command
	{
		private readonly string defaultFolder;

		public WriteFileCmd(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "write";
		public override async Task<string> Execute(List<string> parms)
		{
			var filePath = Path.Combine(defaultFolder, parms[0]);
			await File.WriteAllTextAsync(filePath, parms[1]);
			return filePath;
		}
	}
	class ReadFileCmd : Command
	{
		private readonly string defaultFolder;

		public ReadFileCmd(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "read";
		public override async Task<string> Execute(List<string> parms)
		{
			var filePath = Path.Combine(defaultFolder, parms[0]);
			return await File.ReadAllTextAsync(filePath);
		}
	}
}
