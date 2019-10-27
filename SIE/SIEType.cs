using NodaTime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SIE
{
	public abstract class SIERecord
	{
		public abstract string Tag { get; }
		public abstract void Read(string[] cells);

		public static LocalDate ParseDate(string date) => LocalDate.FromDateTime(DateTime.ParseExact(date, "yyyyMMdd", null));
		public static decimal ParseDecimal(string number) => decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture);
		public static string FormatDate(LocalDate date) => date.ToString("yyyyMMdd", null);

		public static string[] ParseLine(string line)
		{
			var index = 0;
			while (index < line.Length)
			{
				index = line.IndexOf('"', index);
				if (index < 0)
					break;
				var next = line.IndexOf('"', index + 1);
				if (next < 0)
					break;
				var modified = line.Substring(index, next - index + 1).Replace(' ', (char)255);
				line = line.Remove(index) + modified + line.Substring(next + 1);
				index = next + 1;
			}
			var split = line.Split(' ').Select(o => o.Replace((char)255, ' ')).ToList();
			return split.ToArray();
		}

		public static async Task<RootRecord> Read(string path)
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			var encoding = Encoding.GetEncoding(865); // "IBM865");
			using (var sr = new StreamReader(path, encoding))
			{
				return await Read(sr);
			}
		}

		public static async Task<RootRecord> Read(StreamReader sr)
		{
			var types = typeof(SIERecord).Assembly.GetTypes().Where(t => typeof(SIERecord).IsAssignableFrom(t)).ToList();
			var excludeTypes = new[] { typeof(SIERecord), typeof(RootRecord), typeof(UnknownRecord) };
			types = types.Except(excludeTypes).ToList();

			SIERecord Construct(Type type) => type.GetConstructor(new Type[] { }).Invoke(new object[] { }) as SIERecord;
			var tagMap = types.Select(o => new { Tag = Construct(o).Tag, Type = o }).ToDictionary(o => o.Tag, o => o.Type);

			var hierarchy = new Stack<SIERecord>();
			var root = new RootRecord();
			hierarchy.Push(root);
			SIERecord current = null;
			var errors = new List<(Exception exception, string line)>();
			while (!sr.EndOfStream)
			{
				var line = (await sr.ReadLineAsync()).Trim();
				if (line.StartsWith("{"))
				{
					if (current is IWithChildren)
						hierarchy.Push(current);
					else
						throw new Exception($"{current.GetType()} does not implement {nameof(IWithChildren)}");
				}
				else if (line.StartsWith("}"))
					hierarchy.Pop();
				else
				{
					var cells = ParseLine(line);
					if (cells[0].StartsWith("#"))
					{
						var tag = cells[0].Substring(1);
						if (!tagMap.TryGetValue(tag, out var type))
							type = typeof(UnknownRecord);
						var instance = Construct(type);
						try
						{
							instance.Read(cells);
						}
						catch (Exception ex)
						{
							errors.Add((ex, line));
						}
						if (instance is UnknownRecord unknown && unknown.Tag == "SIETYP" && unknown.Data[1] != "4")
						{
							throw new NotImplementedException($"Only SIE v4 is supported (was {unknown.Data[1]})");
						}
						(hierarchy.Peek() as IWithChildren).Children.Add(instance);
						current = instance;
					}
				}
			}
			if (errors.Any())
				throw new AggregateException(string.Join("\n", errors.Select(o => $"{o.exception.Message}: {o.line}")));
			return root;
		}

		public string ToHierarchicalString()
		{
			var sbResult = new StringBuilder();
			RecBuild(this, sbResult, "");

			void RecBuild(SIERecord item, StringBuilder sb, string indent)
			{
				if (!(item is RootRecord))
				{
					sb.Append(indent);
					sb.Append(item.ToString());
					sb.Append('\n');
				}
				if (item is IWithChildren parent)
				{
					indent += " ";
					foreach (var child in parent.Children)
					{
						RecBuild(child, sb, indent);
					}
					indent.Remove(0, 1);
				}
			}

			return sbResult.ToString();
		}
	}

	public interface IWithChildren
	{
		List<SIERecord> Children { get; }
	}

	public class RootRecord : SIERecord, IWithChildren
	{
		public List<SIERecord> Children { get; set; } = new List<SIERecord>();
		public override string Tag => "ROOT";
		public override void Read(string[] cells)
		{
		}
	}

	public class UnknownRecord : SIERecord
	{
		public string[] Data { get; set; }
		private string tag = "";
		public override string Tag => tag;
		public override void Read(string[] cells)
		{
			Data = cells;
			tag = (cells.FirstOrDefault() ?? "").StartsWith("#") ? cells[0].Substring(1) : "";
		}
		public override string ToString()
		{
			return string.Join("\t", Data);
		}
	}



	public class Account : SIERecord
	{
		// #KONTO 84710 "Räntebidrag"
		public override string Tag { get => "KONTO"; }
		public int AccountId { get; set; }
		public string AccountName { get; set; }
		public override void Read(string[] cells)
		{
			AccountId = int.Parse(cells[1]);
			AccountName = cells[2].Trim('"');
		}
		public override string ToString()
		{
			return $"{Tag}: {AccountId} ({AccountName})";
		}
	}
}
