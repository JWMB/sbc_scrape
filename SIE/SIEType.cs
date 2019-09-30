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

						}
						(hierarchy.Peek() as IWithChildren).Children.Add(instance);
						current = instance;
					}
				}
			}
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
		public override string Tag { get => ""; }
		public override void Read(string[] cells)
		{
			Data = cells;
		}
		public override string ToString()
		{
			return string.Join("\t", Data);
		}
	}

	public class VoucherRecord : SIERecord, IWithChildren
	{
		//#VER AR6297 1 20190210 ""
		//{
		//	#TRANS 27180 {} -2047.00 20190210 "Skatteverket"
		//}
		private static Regex rxType = new Regex(@"(\D+)(\d+)");
		private static Dictionary<string, string> codeToName = new Dictionary<string, string> {
			{ "AR", "AR" },
			{ "AV", "AV" },
			{ "BS", "BS" },
			{ "CR", "CR" },
			{ "FAS", "FAS" },
			{ "LON", "Salary" },
			{ "LR", "LR" },
			{ "MA", "Anulled" },
			{ "PE", "Accrual" },
			{ "SLR", "SLR" },
		};


		public string VoucherTypeCode { get; set; }
		public string VoucherForId { get; set; }
		public int Unknown1 { get; set; }
		public LocalDate Date { get; set; }
		public string Unknown2 { get; set; }

		public override string Tag { get => "VER"; }

		public List<SIERecord> Children { get; set; } = new List<SIERecord>();

		public List<TransactionRecord> Transactions { get => Children.Where(o => o is TransactionRecord).Cast<TransactionRecord>().ToList(); }
		public override void Read(string[] cells)
		{
			var match = rxType.Match(cells[1]);
			VoucherTypeCode = match?.Groups[1].Value ?? "N/A";
			VoucherForId = match?.Groups[2].Value ?? "";
			Unknown1 = int.Parse(cells[2]);
			Date = ParseDate(cells[3]);
			Unknown2 = cells[4].Trim('"');
		}
		public override string ToString()
		{
			return $"{Tag} {VoucherTypeCode}{VoucherForId} {Unknown1} {FormatDate(Date)} {Unknown2}";
		}
	}

	public class TransactionRecord : SIERecord
	{
		// #TRANS 27180 {} -2047.00 20190210 "Skatteverket"
		public override string Tag { get => "TRANS"; }
		public int AccountId { get; set; }
		public string Unknown { get; set; }
		public decimal Amount { get; set; }
		public LocalDate Date { get; set; }
		public string Company { get; set; }
		public override void Read(string[] cells)
		{
			AccountId = int.Parse(cells[1]);
			Unknown = cells[2];
			Amount = ParseDecimal(cells[3]);
			Date = ParseDate(cells[4]);
			Company = cells[5].Trim('"');
		}
		public override string ToString() => $"{Tag} {AccountId} {Unknown} {Amount} {FormatDate(Date)} {Company}";
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
