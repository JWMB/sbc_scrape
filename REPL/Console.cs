using System;
using System.Collections.Generic;
using System.Text;

namespace REPL
{
	public abstract class ConsoleBase
	{
		public abstract ConsoleKeyInfo ReadKey(bool intercept);
		public abstract void RewriteLine(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null);
		public abstract void WriteLine(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null);
		public void Write(char c, ConsoleColor? foreColor = null, ConsoleColor? backColor = null) => Write(c.ToString(), foreColor, backColor);
		public abstract void Write(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null);
		public abstract (int X, int Y) CursorPosition { set; get; }
		public int CursorX { get => CursorPosition.X; set => CursorPosition = (value, CursorPosition.Y); }
		public (int X, int Y) CursorPositionWrapped { set => CursorPosition = (value.X % Window.Width, value.Y + value.X / Window.Width); }
		public abstract (int Top, int Width, int Height) Window { get; }
	}

	public class MyConsole : ConsoleBase
	{
		//TODO: do we need a "virtual dom" and flush changes (not too often, e.g. with progress updates)? System.Console is very slow...
		public override ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

		public override (int X, int Y) CursorPosition
		{
			get => (Console.CursorLeft, Console.CursorTop);
			set { Console.CursorLeft = value.X; Console.CursorTop = value.Y; }
		}

		public override (int Top, int Width, int Height) Window => (Console.WindowTop, Console.WindowWidth, Console.WindowHeight);

		//TODO: we need to know how long the line currently is so we can space out the rest of the line
		public override void RewriteLine(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null) =>
			Write(() => {
				CursorPosition = (0, CursorPosition.Y - 1);
				Console.WriteLine(text);
			}, foreColor, backColor);

		public override void Write(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null) =>
			Write(() => Console.Write(text), foreColor, backColor);

		public override void WriteLine(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null) =>
			Write(() => Console.WriteLine(text), foreColor, backColor);

		private void Write(Action writer, ConsoleColor? foreColor = null, ConsoleColor? backColor = null)
		{
			ConsoleColor? orgFg = null;
			if (foreColor != null)
			{
				orgFg = Console.ForegroundColor;
				Console.ForegroundColor = foreColor.Value;
			}
			ConsoleColor? orgBg = null;
			if (backColor != null)
			{
				orgBg = Console.BackgroundColor;
				Console.BackgroundColor = backColor.Value;
			}

			writer();

			if (orgFg != null)
				Console.ForegroundColor = orgFg.Value;
			if (orgBg != null)
				Console.BackgroundColor = orgBg.Value;
		}
	}
}
