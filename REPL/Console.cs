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
		public override ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);

		public override (int X, int Y) CursorPosition
		{
			get => (Console.CursorLeft, Console.CursorTop);
			set { Console.CursorLeft = value.X; Console.CursorTop = value.Y; }
		}

		public override (int Top, int Width, int Height) Window => (Console.WindowTop, Console.WindowWidth, Console.WindowHeight);

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
			ConsoleColor? orgFore = null;
			if (foreColor != null)
			{
				orgFore = Console.ForegroundColor;
				Console.ForegroundColor = foreColor.Value;
			}
			ConsoleColor? orgBack = null;
			if (backColor != null)
			{
				orgBack = Console.BackgroundColor;
				Console.BackgroundColor = backColor.Value;
			}

			writer();

			if (orgFore != null)
				Console.ForegroundColor = orgFore.Value;
			if (orgBack != null)
				Console.BackgroundColor = orgBack.Value;
		}
	}
}
