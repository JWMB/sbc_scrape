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
			//TODO: accept other paths than relative to defaultFolder
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
			//TODO: accept other paths than relative to defaultFolder
			return File.ReadAllText(filePath);
		}
	}

	public class WriteFiles : Command
	{
		private readonly string defaultFolder;
		public WriteFiles(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "writefiles";
		public override async Task<object> Evaluate(List<object> parms)
		{
			//var folder = Path.Combine(defaultFolder, (string)parms[0]);
			var folder = defaultFolder;

			var result = new List<(string, long)>();
			var dictParm = parms[0];
			var t = dictParm.GetType();
			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>) && t.GetGenericArguments()[0] == typeof(string))
			{
				var dict = dictParm as System.Collections.IDictionary;
				foreach (var key in dict.Keys)
				{
					var filePath = Path.Combine(folder, (string)key);
					File.WriteAllText(filePath, dict[key].ToString());
					result.Add((filePath, dict[key].ToString().Length));
				}
			}
			return string.Join("\n", result.Select(r => $"{r.Item1}: {r.Item2}"));
		}
	}
}
