using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace REPL
{
	public interface IReadConsole
	{
		string Read();
	}

	public class ReadConsoleSimple : IReadConsole
	{
		private readonly string prompt;
		public ReadConsoleSimple(string prompt) => this.prompt = prompt;
		public string Read()
		{
			Console.Write(prompt);
			return Console.ReadLine();
		}
	}

	public class ReadConsoleByChar : IReadConsole
	{
		//TODO: doesn't handle console window resize

		private readonly string prompt;
		private readonly List<string> commandHistory;
		private int indexInHistory;

		public ReadConsoleByChar(string prompt, List<string> commandHistory)
		{
			this.prompt = prompt;
			this.commandHistory = commandHistory;
			indexInHistory = commandHistory.Count;
		}

		private void ShowDebug(string input, int position)
		{
			var current = (Console.CursorLeft, Console.CursorTop);
			var numLines = input.Length / Console.WindowWidth;
			var lineStartY = (Console.CursorTop > Console.WindowTop + Console.WindowHeight - 4)
				? Console.WindowTop
				: Console.WindowTop + Console.WindowHeight - numLines - 2;
			Console.SetCursorPosition(0, lineStartY);

			//Text:
			Console.ForegroundColor = ConsoleColor.Blue;
			var numCharsToEndOfLastLine = Console.WindowWidth - input.Length % Console.WindowWidth;
			Console.Write(input + string.Join("", Enumerable.Range(0, numCharsToEndOfLastLine).Select(i => " ")));
			Console.ForegroundColor = ConsoleColor.White;

			//Cursor:
			Console.BackgroundColor = ConsoleColor.Red;
			SetCursorPosition(position, lineStartY);
			Console.Write(position < input.Length ? input[position] : ' ');
			Console.BackgroundColor = ConsoleColor.Black;

			Console.SetCursorPosition(current.CursorLeft, current.CursorTop);
		}

		private static void SetCursorPosition(int positionOnLine, int lineStartY)
		{
			Console.SetCursorPosition(positionOnLine % Console.WindowWidth, lineStartY + positionOnLine / Console.WindowWidth);
		}

		private void RerenderLine(List<char> input, int lineStartY, int formerInputLength = -1)
		{
			SetCursorPosition(prompt.Length, lineStartY);
			var lengthToBlank = formerInputLength == -1 ? input.Count : formerInputLength;
			Console.Write(string.Join("", Enumerable.Range(0, lengthToBlank).Select(i => " ")));
			SetCursorPosition(prompt.Length, lineStartY);
			Console.Write(string.Join("", input));
		}

		public string Read()
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Write(prompt);
			Console.ForegroundColor = ConsoleColor.White;
			var input = new List<char>();
			var lineStartY = Console.CursorTop;
			var position = 0;

			string InputStr() => string.Join("", input);

			while (true)
			{
				ShowDebug(InputStr(), position);

				var key = Console.ReadKey(true);

				//Note: Ctrl+V is handled as if text was coming one keystroke at a time (ie multiple ReadKey's)

				if (key.Key == ConsoleKey.Enter)
				{
					if (InputStr().Trim().Length > 0 && (indexInHistory == commandHistory.Count || commandHistory[indexInHistory] != InputStr()))
					{
						//If wrote a new cmd or we make any change to an old one, it's appended to history and index is set to that last one.
						commandHistory.Add(InputStr());
						indexInHistory = commandHistory.Count;
					}
					else if (indexInHistory < commandHistory.Count)
						indexInHistory++;
					break;
				}
				else if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow)
				{
					var newIndexInHistory = Math.Max(0, Math.Min(indexInHistory + (key.Key == ConsoleKey.UpArrow ? -1 : 1), commandHistory.Count));
					if (newIndexInHistory != indexInHistory)
					{
						var formerLength = input.Count;

						//As long as we just select an old command and press ENTER, keep the index of that slot.
						if (newIndexInHistory == commandHistory.Count)
						{
							indexInHistory = newIndexInHistory;
							input.Clear();
						}
						else
						{
							if (indexInHistory == commandHistory.Count && InputStr().Trim().Length > 0)
							{
								commandHistory.Add(InputStr());
							}
							indexInHistory = newIndexInHistory;
							input = commandHistory[indexInHistory].ToList();
						}
						RerenderLine(input, lineStartY, formerLength);
						position = input.Count;
						SetCursorPosition(prompt.Length + position, lineStartY);
					}
				}
				else if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow)
				{
					position = Math.Max(0, Math.Min(position + (key.Key == ConsoleKey.LeftArrow ? -1 : 1), input.Count));
					SetCursorPosition(prompt.Length + position, lineStartY);
				}
				else if (key.Key == ConsoleKey.Backspace)
				{
					if (position > 0)
					{
						position--;
						input.RemoveAt(position);
						RerenderLine(input, lineStartY, input.Count + 1);
						SetCursorPosition(prompt.Length + position, lineStartY);
					}
					continue;
				}

				if (key.KeyChar == '\0')
					continue;

				// Add character:
				if (position >= input.Count)
				{
					Console.Write(key.KeyChar);
					input.Add(key.KeyChar);
				}
				else
				{
					input.Insert(position, key.KeyChar);
					Console.CursorLeft = prompt.Length;
					Console.Write(InputStr());
					SetCursorPosition(prompt.Length + position + 1, lineStartY);
				}
				position++;
			}
			Console.WriteLine("");

			return InputStr();
		}
	}
}
