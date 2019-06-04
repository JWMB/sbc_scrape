using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace REPL
{
	public class NullCmd : Command
	{
		public override string Id => "";
		public override async Task<object> Evaluate(List<object> parms)
		{
			return parms.Count == 0 ? "" : $"{parms.First()} not found";
		}
	}

	public class QuitCmd : Command, IQuitCommand
	{
		public override string Id => "q";
		public override async Task<object> Evaluate(List<object> parms)
		{
			return "Goodbye!";
		}
	}
	public class ListCmd : Command
	{
		private readonly IEnumerable<Command> commands;
		public override string Id => "l";
		public ListCmd(IEnumerable<Command> commands) => this.commands = commands;
		public override async Task<object> Evaluate(List<object> parms)
		{
			return "Available commands:\n"
				+ string.Join("\n",
				commands.Select(c => $"{c.GetType().Name.Replace("Cmd", "")}: {c.Id}\n{c.Help}"));
		}
	}

	public class CSVCmd : Command
	{
		public CSVCmd() { }
		public override string Id => "csv";
		public override async Task<object> Evaluate(List<object> parms)
		{
			if (parms[0] is string)
			{
				var type = AppDomain.CurrentDomain.GetAssemblies()
					.Select(a => a.GetType(parms[1] as string)).FirstOrDefault();
				return ServiceStack.Text.CsvSerializer.DeserializeFromString(type, parms[0] as string);
			}
			return ServiceStack.Text.CsvSerializer.SerializeToString(parms[0]);
		}
	}


	public class WriteFileCmd : Command
	{
		private readonly string defaultFolder;

		public WriteFileCmd(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "write";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var filePath = Path.Combine(defaultFolder, (string)parms[0]);
			File.WriteAllText(filePath, parms[1].ToString());
			//TODO: where is File.WriteAllTextAsync?
			return filePath;
		}
	}
	public class ReadFileCmd : Command
	{
		private readonly string defaultFolder;

		public ReadFileCmd(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "read";
		public override async Task<object> Evaluate(List<object> parms)
		{
			var filePath = Path.Combine(defaultFolder, (string)parms[0]);
			//TODO: where is File.ReadAllTextAsync?
			return File.ReadAllText(filePath);
		}
	}
}
