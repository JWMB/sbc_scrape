using Newtonsoft.Json;
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
		public string Evaluate(List<object> parms)
		{
			return parms.Count == 0 ? "" : $"{parms.First()} not found";
		}
	}

	public class QuitCmd : Command, IQuitCommand
	{
		public override string Id => "q";
		public string Evaluate()
		{
			return "Goodbye!";
		}
	}
	public class ListCmd : Command
	{
		private readonly IEnumerable<Command> commands;
		public override string Id => "l";
		public ListCmd(IEnumerable<Command> commands) => this.commands = commands;
		public string Evaluate()
		{
			return "Available commands:\n"
				+ string.Join("\n",
				commands.Select(c => $"{c.GetType().Name.Replace("Cmd", "")}: {c.Id}\n{c.Help}"));
		}
		public Task<string> Evaluate(int someValue)
		{
			return Task.FromResult($"Hello {someValue}");
		}
	}

	//TODO: will require switching from Console.ReadLine
	//public class CommandHistory : Command
	//{
	//	class ExecutedCommand
	//	{
	//		public string Command { get; set; }
	//		public DateTime Timestamp { get; set; }
	//	}

	//	private List<ExecutedCommand> commands = new List<ExecutedCommand>();
	//	private readonly string filepathHistory;

	//	public void Append(string command)
	//	{
	//		commands.Add(new ExecutedCommand { Command = command, Timestamp = DateTime.Now });
	//		var maxCount = 50;
	//		if (commands.Count > maxCount)
	//			commands = commands.Skip(commands.Count - maxCount).ToList();
	//		File.WriteAllText(filepathHistory, JsonConvert.SerializeObject(commands));
	//	}

	//	public override string Id => "history";
	//	public CommandHistory(string filepathHistory) {
	//		this.filepathHistory = filepathHistory;
	//		if (File.Exists(filepathHistory))
	//			commands = JsonConvert.DeserializeObject<List<ExecutedCommand>>(File.ReadAllText(filepathHistory));
	//	}
	//	public override async Task<object> Evaluate(List<object> parms)
	//	{
	//		return commands;
	//	}
	//}

	public class CSVCmd : Command
	{
		private readonly CsvHelper.Configuration.CsvConfiguration? csvConfig;

		public CSVCmd(CsvHelper.Configuration.CsvConfiguration? csvConfig = null)
		{
			if (csvConfig == null)
			{
				var cultureInfo = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
				cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
				cultureInfo.NumberFormat.NumberGroupSeparator = "";
				csvConfig = new CsvHelper.Configuration.CsvConfiguration(cultureInfo)
				{
					Delimiter = "\t",
				};
			}

			this.csvConfig = csvConfig;
		}

		public override string Id => "csv";
		public Task<object> Evaluate(string typeName, string str)
		{
			if (string.IsNullOrEmpty(typeName))
				throw new ArgumentNullException($"{nameof(typeName)}");
			var type = AppDomain.CurrentDomain.GetAssemblies()
				.Select(a => a.GetTypes().FirstOrDefault(t => t.Name == typeName)).FirstOrDefault(t => t != null);
			if (type == null)
				throw new ArgumentException($"Type not found: {typeName}");

			using var reader = new StringReader(str);
			using var csv = new CsvHelper.CsvReader(reader, csvConfig);
			return Task.FromResult<object>(csv.GetRecords(type).ToList());

		}
		public Task<object> Evaluate(IEnumerable<object> objects)
		{
			using (var writer = new StringWriter())
			using (var csv = new CsvHelper.CsvWriter(writer, csvConfig))
			{
				csv.WriteRecords(objects);
				return Task.FromResult<object>(writer.ToString());
			}
		}
	}


	public class WriteFileCmd : Command
	{
		private readonly string defaultFolder;

		public WriteFileCmd(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "write";
		public async Task<string> Evaluate(string path, object value)
		{
			var filePath = Path.Combine(defaultFolder, path);
			await File.WriteAllTextAsync(filePath, value.ToString());
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
		public async Task<byte[]> Evaluate(string path)
		{
			var filePath = Path.Combine(defaultFolder, path);
			//TODO: where is File.ReadAllTextAsync?
			//TODO: accept other paths than relative to defaultFolder
			return await File.ReadAllBytesAsync(filePath);
		}
	}

	public class WriteFiles : Command
	{
		private readonly string defaultFolder;
		public WriteFiles(string defaultFolder) => this.defaultFolder = defaultFolder;
		public override string Id => "writefiles";
		public string Evaluate(object dictParm)
		{
			//var folder = Path.Combine(defaultFolder, (string)parms[0]);
			var folder = defaultFolder;

			var result = new List<(string, long)>();
			//var dictParm = parms[0];
			var t = dictParm.GetType();
			if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>) && t.GetGenericArguments()[0] == typeof(string))
			{
				var dict = (System.Collections.IDictionary)dictParm;
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
