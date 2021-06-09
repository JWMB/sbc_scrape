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
	// http://www.sie.se/wp-content/uploads/2014/01/SIE_filformat_ver_4B_080930.pdf
	/*
	 * ADRESSAdressupgifter för det exporterade företagetFormat:
	 * 
	 */
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
			var encoding = DetectEncoding(path);
			using var sr = new StreamReader(path, encoding);
			return await Read(sr);
		}

		private static Encoding DetectEncoding(string path)
		{
			//var bufLen = 1024;
			//var buf = new byte[bufLen];
			//using (var fs = File.OpenRead(path))
			//{
			//	var read = fs.Read(buf, 0, bufLen);
			//	for (int i = 0; i < read; i++)
			//	{
			//		//if (buf[i] == (byte)0x10)
			//	}
			//	fs.Close();
			//}

			//foreach (var line in lines)
			//{
			//	if (line.StartsWith("#KONTO 11110"))
			//	{ }
			//	if (line.StartsWith("#KONTO 11201"))
			//		break;
			//}
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			return Encoding.GetEncoding(865); // "IBM865");
		}

		public static async Task<RootRecord> Read(StreamReader sr)
		{
			var types = typeof(SIERecord).Assembly.GetTypes().Where(t => typeof(SIERecord).IsAssignableFrom(t) && !t.IsAbstract).ToList();
			var excludeTypes = new[] { typeof(SIERecord), typeof(RootRecord), typeof(UnknownRecord) };
			types = types.Except(excludeTypes).ToList();

			static SIERecord Construct(Type type) => (SIERecord)(type.GetConstructor(new Type[] { })?.Invoke(new object[] { }) ?? throw new Exception($"Invalid type {type}"));
			var tagMap = types.Select(o => new { Construct(o).Tag, Type = o }).ToDictionary(o => o.Tag, o => o.Type);

			var hierarchy = new Stack<SIERecord>();
			var root = new RootRecord();
			hierarchy.Push(root);
			SIERecord? current = null;
			var errors = new List<(Exception exception, string line)>();
			while (!sr.EndOfStream)
			{
				var line = ((await sr.ReadLineAsync()) ?? "").Trim();
				if (line.StartsWith("{"))
				{
					if (current is IWithChildren)
						hierarchy.Push(current);
					else
						throw new Exception($"{current?.GetType().FullName ?? "N/A"} does not implement {nameof(IWithChildren)}");
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
						((IWithChildren)hierarchy.Peek()).Children.Add(instance);
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

			static void RecBuild(SIERecord item, StringBuilder sb, string indent)
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

		public static T Parse<T>(string val, T defaultValueElseThrow = default)
		{
			var parsed = Parse(typeof(T), val, defaultValueElseThrow);
			if (parsed == null) throw new NullReferenceException($"{val}");
			return (T)parsed;
		}

		public static object? Parse(Type type, string val, object? defaultValueElseThrow)
		{
			object converted;
			try
			{
				if (type == typeof(int))
					converted = int.Parse(val);
				else if (type == typeof(decimal))
					converted = ParseDecimal(val);
				else if (type == typeof(LocalDate))
					converted = ParseDate(val);
				else if (type == typeof(bool))
					converted = val.Length == 1 ? val == "1" : bool.Parse(val);
				else if (type == typeof(string))
					converted = val.Trim('"');
				else
					throw new NotImplementedException($"Parsing type {type.Name} not supported");
				return Convert.ChangeType(converted, type);
			}
			catch (Exception ex)
			{
				if (defaultValueElseThrow == null) throw new FormatException($"Couldn't parse {val} to {type.Name}", ex);
				return defaultValueElseThrow;
			}
		}

		protected void Populate(IEnumerable<string> cells, IEnumerable<string> propertyNames)
		{
			Populate(cells, propertyNames.Select(p => GetType().GetProperty(p)).OfType<System.Reflection.PropertyInfo>(), this);
		}

		public static void Populate(IEnumerable<string> cells, IEnumerable<System.Reflection.PropertyInfo> properties, object targetObject)
		{
			for (int i = 0; i < Math.Min(cells.Count(), properties.Count()); i++)
			{
				var prop = properties.Skip(i).Take(1).Single();
				var val = cells.Skip(i).Take(1).Single();
				var converted = Parse(prop.PropertyType, val, null);
				//throw new FormatException($"{val} is not of type {type.Name}");
				prop.SetValue(targetObject, converted);
			}
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
		public override void Read(string[] cells) { }

		public override string ToString()
		{
			var reportPeriod = Children.OfType<ReportPeriodRecord>().FirstOrDefault();
			return $"{Tag} {reportPeriod?.Start.ToSimpleDateString()}";
		}
	}

	public class UnknownRecord : SIERecord
	{
		public string[] Data { get; set; } = new string[] { };
		private string tag = "";
		public override string Tag => tag;
		public override void Read(string[] cells)
		{
			Data = cells;
			tag = (cells.FirstOrDefault() ?? "").StartsWith("#") ? cells[0].Substring(1) : "";
		}
		public override string ToString() => string.Join("\t", Data);
	}

	public class ReportPeriodRecord : SIERecord
	{
		public override string Tag { get => "RAR"; }

		public int YearOffset { get; set; }
		public LocalDate Start { get; set; }
		public LocalDate End { get; set; }

		public override void Read(string[] cells)
		{
			Populate(cells.Skip(1), new[] { nameof(YearOffset), nameof(Start), nameof(End) });
		}
	}

	public abstract class BalanceRecord : SIERecord
	{
		public int YearOffset { get; set; }
		public int AccountId { get; set; }
		public decimal Amount { get; set; }

		public override void Read(string[] cells)
		{
			Populate(cells.Skip(1), new[] { nameof(YearOffset), nameof(AccountId), nameof(Amount) });
		}
		public override string ToString() => $"{Tag} {YearOffset} {AccountId} {Amount}";
	}
	public class IngoingBalanceRecord : BalanceRecord
	{
		public override string Tag { get => "IB"; }
	}
	public class OutgoingBalanceRecord : BalanceRecord
	{
		public override string Tag { get => "UB"; }
	}

	public class ResultRecord : SIERecord
	{
		public override string Tag { get => "RES"; }

		public int YearOffset { get; set; }
		public int AccountId { get; set; }
		public decimal Amount { get; set; }

		public override void Read(string[] cells)
		{
			Populate(cells.Skip(1), new[] { nameof(YearOffset), nameof(AccountId), nameof(Amount) });
		}
	}


	public class AddressRecord : SIERecord
	{
		// #ADRESS "SvenSvensson"  "Box 21""21120   MALMÖ"  "040-12345"
		// kontakt utdelningsadr postadr tel
		public override string Tag { get => "ADRESS"; }
		public string Contact { get; set; } = string.Empty;
		public string Address { get; set; } = string.Empty;
		public string PostalAddress { get; set; } = string.Empty;
		public string PhoneNumber { get; set; } = string.Empty;

		public override void Read(string[] cells)
		{
			Populate(cells.Skip(1), new[] { nameof(Contact), nameof(Address), nameof(PostalAddress), nameof(PhoneNumber) });
		}
		public override string ToString() => $"{Tag}: {Contact} ({Address})";
	}

	public class IndustryRecord : SIERecord
	{
		// #BKOD  SNI-kod
		public override string Tag { get => "BKOD"; }
		public string SNI { get; set; } = string.Empty;
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(SNI) });
		public override string ToString()=>  $"{Tag}: {SNI})";
	}
	public class DimensionRecord : SIERecord
	{
		//# DIM    dimensionsnr    namn  # DIM    1   "Avdelning"
		public override string Tag { get => "DIM"; }
		public int DimensionId { get; set; }
		public string Name { get; set; } = string.Empty;
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(DimensionId), nameof(Name) });
		public override string ToString()=> $"{Tag}: {DimensionId} {Name})";
	}

	public class UnitRecord : SIERecord
	{
		//#ENHET  kontonr enhet
		public override string Tag { get => "ENHET"; }
		public int AccountId { get; set; }
		public string Unit { get; set; } = string.Empty;
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(AccountId), nameof(Unit) });
		public override string ToString() => $"{Tag}: {AccountId} {Unit})";
	}

	public class FlagRecord : SIERecord
	{
		//#FLAGGA x
		public override string Tag { get => "FLAGGA"; }
		public bool Value { get; set; }
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(Value) });
		public override string ToString() => $"{Tag}: {Value})";
	}

	public class CompanyNameRecord : SIERecord
	{
		//#FNAMN  företagsnamn
		public override string Tag { get => "FNAMN"; }
		public string Value { get; set; } = string.Empty;
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(Value) });
		public override string ToString() => $"{Tag}: {Value})";
	}

	public class CompanyIdRecord : SIERecord
	{
		//#FNR    företagsid
		public override string Tag { get => "FNR"; }
		public int Value { get; set; }
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(Value) });
		public override string ToString() => $"{Tag}: {Value})";
	}

	public class FormatRecord : SIERecord
	{
		//#FORMAT PC8
		public override string Tag { get => "FORMAT"; }
		public string Value { get; set; } = string.Empty;
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(Value) });
		public override string ToString() => $"{Tag}: {Value})";
	}


	public class AccountRecord : SIERecord
	{
		// #KONTO 84710 "Räntebidrag"
		public override string Tag { get => "KONTO"; }
		public int AccountId { get; set; }
		public string AccountName { get; set; } = string.Empty;
		public override void Read(string[] cells) => Populate(cells.Skip(1), new[] { nameof(AccountId), nameof(AccountName) });
		public override string ToString() => $"{Tag}: {AccountId} ({AccountName})";
	}
}
