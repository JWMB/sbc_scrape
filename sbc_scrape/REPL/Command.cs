using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.REPL
{

	interface IQuitCommand
	{ }
	interface IUpdateCommandList
	{
		IEnumerable<Command> UpdateCommandList(IEnumerable<Command> currentCommandList);
	}

	abstract class Command
	{
		public abstract string Id { get; }
		public abstract Task<string> Execute(List<string> parms);

		public virtual string Help { get; }

		private static NullCmd nullCmd = new NullCmd();
		public static async Task<(Command, string)> ExecuteCommand(string str, IEnumerable<Command> cmds, string previousResult = null)
		{
			var split = str.Split(' ').ToList();
			var found = cmds.FirstOrDefault(c => c.Id == split[0]);

			if (previousResult != null)
			{
				if (found == null && split.Count == 1) //when piping to a file, auto-insert "write" as the command
				{
					split.Insert(0, "write");
					found = cmds.FirstOrDefault(c => c.Id == split[0]);
				}
			}

			var skip = 1;
			if (found == null) //include missing command in arguments passed to nullCmd
			{
				skip = 0;
				found = cmds.FirstOrDefault(c => c is NullCmd) ?? nullCmd;
			}

			//Pipe handling
			var pipeIndex = split.IndexOf(">");
			string theRest = null;
			if (pipeIndex > 0)
			{
				theRest = string.Join(" ", split.Skip(pipeIndex + 1)).Trim();
				split = split.Take(pipeIndex).ToList();
			}
			if (previousResult != null)
				split.Add(previousResult);

			string result = null;
			try
			{
				result = await found.Execute(split.Skip(skip).ToList());
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error for {string.Join(' ', split)}:\n{ex.Message}");
			}
			if (!string.IsNullOrEmpty(theRest))
			{
				return await ExecuteCommand(theRest, cmds, result);
			}
			return (found, result);
		}

		public static async Task RunCommandLoop(IEnumerable<Command> cmds)
		{
			var quitCmd = cmds.FirstOrDefault(c => c is QuitCmd);
			var listCmd = cmds.FirstOrDefault(c => c is ListCmd);
			var help = (listCmd == null && quitCmd == null) ? "" : $"{(quitCmd == null ? "" : $"{quitCmd.Id} to quit")} {(listCmd == null ? "" : $"{listCmd.Id} to list commands")}";
			Console.WriteLine($"Enter command{(help == null ? "" : $"({help})")}:");

			var quitting = false;
			while (!quitting)
			{
				Console.Write("> ");
				var lines = Console.ReadLine().Trim().Split(';').Select(s => s.Trim()).Where(s => s.Length > 0);
				foreach (var line in lines)
				{
					var result = await Command.ExecuteCommand(line, cmds);
					Console.WriteLine(result.Item2);

					//Special commands that modify flow:
					if (result.Item1 is IUpdateCommandList updateCmd)
					{
						var originalTypesList = cmds.Select(c => c.GetType()).ToList();
						cmds = updateCmd.UpdateCommandList(cmds);
						var newCommands = cmds.Where(c => !originalTypesList.Contains(c.GetType()));
						Console.WriteLine($"Added {(string.Join(",", newCommands.Select(c => c.Id)))}");
					}
					if (result.Item1 is IQuitCommand)
					{
						quitting = true;
						break;
					}
				}
			}
		}

		public static bool TryParseArgument<T>(List<string> parms, int index, out T value)
		{
			object parsed = null;
			if (parms != null && parms.Count > index)
			{
				var str = parms[index];
				if (typeof(T) == typeof(long))
				{
					if (long.TryParse(str, out var lr))
						parsed = Convert.ChangeType(lr, typeof(T));
				}
				else if (typeof(T) == typeof(DateTime))
				{
					if (DateTime.TryParse(str, out var p))
						parsed = Convert.ChangeType(p, typeof(T));
				}
			}
			if (parsed == null)
			{
				value = default(T);
				return false;
			}
			value = (T)parsed;
			return true;
		}
		public static T ParseArgument<T>(List<string> parms, int index, T defaultValue)
		{
			if (TryParseArgument<T>(parms, index, out var parsed))
				return parsed;
			return defaultValue;
		}
	}
}
