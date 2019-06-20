using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace REPL
{
	public interface IQuitCommand
	{ }
	public interface IUpdateCommandList
	{
		IEnumerable<Command> UpdateCommandList(IEnumerable<Command> currentCommandList);
	}

	public abstract class Command
	{
		public abstract string Id { get; }
		public abstract Task<object> Evaluate(List<object> parms);

		public virtual string Help { get; }

		private static NullCmd nullCmd = new NullCmd();
		public static async Task<(Command, object)> Evaluate(List<object> input, IEnumerable<Command> cmds, object previousResult = null)
		{
			var found = cmds.FirstOrDefault(c => c.Id == (string)input[0]);

			if (previousResult != null)
			{
				if (found == null && input.Count == 1) //when piping to a file, auto-insert "write" as the command
				{
					input.Insert(0, "write");
					found = cmds.FirstOrDefault(c => c.Id == (string)input[0]);
				}
			}

			var skip = 1;
			if (found == null) //include missing command in arguments passed to nullCmd
			{
				skip = 0;
				found = cmds.FirstOrDefault(c => c is NullCmd) ?? nullCmd;
			}

			//Pipe handling
			var pipeIndex = input.FindIndex(s => s is string && (string)s == ">");
			List<object> theRest = null;
			if (pipeIndex > 0)
			{
				theRest = input.Skip(pipeIndex + 1).ToList();
				input = input.Take(pipeIndex).ToList();
			}
			if (previousResult != null)
				input.Add(previousResult);

			object result = null;
			try
			{
				result = await found.Evaluate(input.Skip(skip).Cast<object>().ToList());
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error for {string.Join(" ", input)}:\n{ex.Message}\n{ex.StackTrace}");
				return (null, "Error");
			}
			if (theRest != null && theRest.Any())
			{
				return await Evaluate(theRest, cmds, result);
			}
			return (found, result);
		}

		public static async Task RunREPL(IEnumerable<Command> cmds)
		{
			var quitCmd = cmds.FirstOrDefault(c => c is QuitCmd);
			var listCmd = cmds.FirstOrDefault(c => c is ListCmd);
			var help = (listCmd == null && quitCmd == null) ? "" : $"{(quitCmd == null ? "" : $"{quitCmd.Id} to quit")} {(listCmd == null ? "" : $"{listCmd.Id} to list commands")}";
			Console.WriteLine($"Enter command{(help == null ? "" : $"({help})")}:");

			var commandHistory = new List<string> { "very old command", "old command" };
			var indexInHistory = commandHistory.Count;
			var quitting = false;
			var prompt = "> ";
			while (!quitting)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.Write(prompt);
				Console.ForegroundColor = ConsoleColor.White;
				var input = new List<char>();
				var position = 0;
				while (true)
				{
					{
						var current = (Console.CursorLeft, Console.CursorTop);
						Console.SetCursorPosition(0, current.CursorTop + 2);

						//Text:
						Console.ForegroundColor = ConsoleColor.Blue;
						Console.Write(string.Join("", input) + string.Join("", Enumerable.Range(input.Count, Console.WindowWidth).Select(i => " ")));
						Console.ForegroundColor = ConsoleColor.White;

						//Cursor:
						Console.BackgroundColor = ConsoleColor.Red;
						Console.CursorLeft = position;
						Console.Write(position < input.Count ? input[position] : ' ');

						Console.BackgroundColor = ConsoleColor.Black;

						Console.SetCursorPosition(current.CursorLeft, current.CursorTop);
					}

					var key = Console.ReadKey(true);
					//Note: Ctrl+V is handled as if text was coming from keyboard (ie multiple keystrokes)

					if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow)
					{
						var newIndexInHistory = Math.Max(0, Math.Min(indexInHistory + (key.Key == ConsoleKey.UpArrow ? -1 : 1), commandHistory.Count));
						//var direction = key.Key == ConsoleKey.UpArrow ? -1 : 1;
						if (newIndexInHistory != indexInHistory) //(direction > 0 && direction <= commandHistory.Count)
						{
							if (indexInHistory == commandHistory.Count)
							{
								commandHistory.Add(string.Join("", input));
							}
							Console.CursorLeft = prompt.Length;
							Console.Write(string.Join("", input.Select(i => " ")));
							indexInHistory = newIndexInHistory;
							input = commandHistory[indexInHistory].ToList();
							Console.CursorLeft = prompt.Length;
							Console.Write(string.Join("", input));
							position = input.Count;
						}
					}
					else if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow)
					{
						position = Math.Max(0, Math.Min(position + (key.Key == ConsoleKey.LeftArrow ? -1 : 1), input.Count));
						Console.CursorLeft = prompt.Length + position;
					}
					else if (key.Key == ConsoleKey.Enter)
						break;
					else if (key.Key == ConsoleKey.Backspace)
					{
						if (position > 0)
						{
							position--;
							input.RemoveAt(position);
							Console.Write(key.KeyChar);
							Console.Write(" ");
							Console.Write(key.KeyChar);
						}
						continue;
					}
					if (key.KeyChar == '\0')
						continue;

					if (position >= input.Count)
					{
						Console.Write(key.KeyChar);
						input.Add(key.KeyChar);
					}
					else
					{
						input.Insert(position, key.KeyChar);
						Console.CursorLeft = prompt.Length;
						Console.Write(string.Join("", input));
						Console.CursorLeft = prompt.Length + position + 1;
					}
					position++;
				}
				Console.WriteLine("");
				//var input = Console.ReadLine();
				var lines = string.Join("", input).Trim().Split(';').Select(s => s.Trim()).Where(s => s.Length > 0);
				foreach (var line in lines)
				{
					var split = line.Split(' ').Cast<object>().ToList();
					var result = await Command.Evaluate(split, cmds);
					Console.WriteLine(result.Item2.ToString());

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

		public static bool TryParseArgument<T>(List<object> parms, int index, out T value)
		{
			object parsed = null;
			if (parms != null && parms.Count > index)
			{
				Func<object, object> convert = null;
				if (typeof(T) == typeof(long))
				{
					convert = o => Convert.ToInt64(o);
					//if (long.TryParse(val, out var lr)
					//	parsed = Convert.ChangeType(lr, typeof(T));
				}
				else if (typeof(T) == typeof(DateTime))
				{
					convert = o => Convert.ToDateTime(o);
					//if (DateTime.TryParse(val, out var p))
					//	parsed = Convert.ChangeType(p, typeof(T));
				}
				else if (typeof(T) == typeof(string))
					convert = o => o.ToString();
				if (convert != null)
				{
					try
					{
						parsed = convert(parms[index]);
					}
					catch { }
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
		public static T ParseArgument<T>(List<object> parms, int index, T defaultValue)
		{
			if (TryParseArgument<T>(parms, index, out var parsed))
				return parsed;
			return defaultValue;
		}
	}
}
