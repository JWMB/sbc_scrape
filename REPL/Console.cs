using System;
using System.Collections.Generic;
using System.Linq;
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

		private string _lastWrite = string.Empty;

		private void _WriteLine(string text)
		{
			_lastWrite = text;
			Console.WriteLine(text);
		}
		private void _Write(string text)
		{
			_lastWrite = text;
			Console.Write(text);
		}

		//Leaky abstraction, we may e.g. have changed window size. Whole lastWrite thingy should be replaced with VDOM anyway
		private List<int> LastWriteLineLengths
		{
			get
			{
				var lengths = _lastWrite.Split('\n').SelectMany(ln => Enumerable.Range(0, ln.Length / Window.Width).Select(o => Window.Width).Concat(new[] { ln.Length % Window.Width }));
				return lengths.ToList();
			}
		}
		private string GetEmptyString(int len) => "".PadLeft(len, ' ');

		public override (int Top, int Width, int Height) Window => (Console.WindowTop, Console.WindowWidth, Console.WindowHeight);

		//TODO: we need to know how long the line currently is so we can space out the rest of the line
		public override void RewriteLine(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null) =>
			Write(() => {
				var lln = LastWriteLineLengths;
				if (lln.Count > 1 || lln.First() > text.Length)
				{
					CursorPosition = (0, CursorPosition.Y - lln.Count);
					foreach (var item in lln)
						_WriteLine(GetEmptyString(item));
					
				}
				CursorPosition = (0, CursorPosition.Y - 1);
				_WriteLine(text);
			}, foreColor, backColor);

		public override void Write(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null) =>
			Write(() => _Write(text), foreColor, backColor);

		public override void WriteLine(string text, ConsoleColor? foreColor = null, ConsoleColor? backColor = null) =>
			Write(() => _WriteLine(text), foreColor, backColor);

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
