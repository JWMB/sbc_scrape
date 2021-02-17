using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace REPL
{
	public class Runner
	{
		private readonly NullCmd nullCmd = new NullCmd();
		private readonly QuitCmd? quitCmd;
		private IEnumerable<Command> cmds;
		private readonly ConsoleBase console;

		public class CallAndResultEventArgs : EventArgs
		{
			public System.Reflection.MethodBase? Method { get; set; }
			public List<object> Arguments { get; set; } = new List<object>();
			public object? Result { get; set; } = null;
		}

		public event EventHandler<CallAndResultEventArgs> CallAndResult;

		public Runner(IEnumerable<Command> cmds)
		{
			quitCmd = cmds.FirstOrDefault(c => c is QuitCmd) as QuitCmd;
			this.cmds = cmds;
			console = new MyConsole();
			PrepareCommands(cmds);
		}

		private void PrepareCommands(IEnumerable<Command> cmds)
		{
			cmds.ToList().ForEach(c => c.Console ??= console);
		}

		public async Task<(Command?, object)> Evaluate(List<object> input, IEnumerable<Command> cmds, object? previousResult = null)
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
			List<object>? theRest = null;
			if (pipeIndex > 0)
			{
				theRest = input.Skip(pipeIndex + 1).ToList();
				input = input.Take(pipeIndex).ToList();
			}
			if (previousResult != null)
				input.Add(previousResult);

			object? result = null;
			try
			{
				var actualArguments = input.Skip(skip).Cast<object>().ToList();
				var methods = found.GetType().GetMethods().Where(o => o.Name == nameof(Command.Evaluate));
				var (method, castArgs) = Binding.BindMethod(methods, actualArguments);
				if (method != null)
				{
					var m = method as System.Reflection.MethodInfo;
					if (m?.ReturnType.BaseType == typeof(Task))
					//if (m?.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), true).Any() == true)
					{
						var task = (Task)method.Invoke(found, castArgs);
						await task.ConfigureAwait(false);

						var resultProperty = task.GetType().GetProperty("Result");
						result = resultProperty.GetValue(task);
					}
					else
						result = method.Invoke(found, castArgs);
					CallAndResult?.Invoke(this, new CallAndResultEventArgs { Method = method, Arguments = castArgs.ToList(), Result = result });
				}
				else
				{
					result = await found.Evaluate(actualArguments);
					CallAndResult?.Invoke(this, new CallAndResultEventArgs { Method = methods.First(), Arguments = actualArguments, Result = result });
				}
			}
			catch (Exception ex)
			{
				console.WriteLine($"Error for {string.Join(" ", input)}:\n{ex}");
				return (null, "Error");
			}
			if (theRest != null && theRest.Any())
			{
				return await Evaluate(theRest, cmds, result);
			}
			return (found, result);
		}

		public async Task RunREPL(IInputReader inputReader, CancellationToken cancel)
		{
			var listCmd = cmds.FirstOrDefault(c => c is ListCmd);
			var help = (listCmd == null && quitCmd == null) ? "" : $"{(quitCmd == null ? "" : $"{quitCmd.Id} to quit")} {(listCmd == null ? "" : $"{listCmd.Id} to list commands")}";
			console.WriteLine($"Enter command{(help == null ? "" : $"({help})")}:");

			var quitting = false;

			while (!quitting)
			{
				if (cancel.IsCancellationRequested)
					return;

				var input = inputReader.Read();

				var lines = input.Trim().Split(';').Select(s => s.Trim()).Where(s => s.Length > 0);
				foreach (var line in lines)
				{
					var split = line.Split(' ').Cast<object>().ToList();
					var result = await Evaluate(split, cmds);
					console.WriteLine(result.Item2?.ToString() ?? "No result.Item2!");

					//Special commands that modify flow:
					if (result.Item1 is IUpdateCommandList updateCmd)
					{
						var originalTypesList = cmds.Select(c => c.GetType()).ToList();
						cmds = updateCmd.UpdateCommandList(cmds);
						var newCommands = cmds.Where(c => !originalTypesList.Contains(c.GetType()));
						PrepareCommands(newCommands);
						console.WriteLine($"Added {(string.Join(",", newCommands.Select(c => c.Id)))}");
					}
					if (result.Item1 is IQuitCommand)
					{
						quitting = true;
						break;
					}
				}
			}
		}
	}
}
