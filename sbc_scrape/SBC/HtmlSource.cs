using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace sbc_scrape.SBC
{
	public abstract class HtmlSource
	{
		public string FilenamePattern => $"{SavedFilePrefix}_{{0}}.html";
		public abstract string SavedFilePrefix { get; }
		public abstract string UrlPath { get; }
		public abstract List<object> ParseObjects(string html);
		public abstract List<object> ReadAllObjects(string folder);

		public static DocumentVersion GetDocumentVersion(string html)
		{
			if (html.StartsWith(@"<html xmlns=""http://www.w3.org/1999/xhtml"">"))
				return DocumentVersion.Pre2020;
			if (html.Contains("ctl00_MainBodyAddRegion_ctl01_DDAccountFrom"))
				return DocumentVersion.Fall2020;
			return DocumentVersion.Spring2020;
		}
	}

	public enum DocumentVersion
	{
		Pre2020,
		Spring2020,
		Fall2020,
	}

	public abstract class HtmlSource<TRow> : HtmlSource
	{
		public abstract List<TRow> Parse(string html);
		public override List<object> ParseObjects(string html) => Parse(html).Cast<object>().ToList();
		public override List<object> ReadAllObjects(string folder) => ReadAll(folder).Cast<object>().ToList();

		public static List<T> ParseDocument<T>(HtmlDocument doc, Func<List<string>, T> deserializeRow, IEnumerable<string> skipColumns = null)
		{
			var docVersion = GetDocumentVersion(doc.DocumentNode.OuterHtml);
			var node = docVersion == DocumentVersion.Pre2020
				? doc.DocumentNode.SelectSingleNode("//table[@class='portal-table']")
				: (docVersion == DocumentVersion.Spring2020
					? doc.DocumentNode.SelectSingleNode("//table[starts-with(@id,'ctl00_MainBodyAddRegion_ctl01_GridView')]")
					: doc.DocumentNode.SelectSingleNode("//table[starts-with(@id,'ctl00_MainBodyAddRegion_ctl01_GridPHResult')]"));
			// //table[@id='ctl00_MainBodyAddRegion_ctl01_GridViewUrval']
			if (node == null)
				throw new FormatException("Couldn't find table node");
			node = node.ChildNodes.FirstOrDefault(n => n.Name == "tbody") ?? node; //Some variants have no tbody, but tr directly under

			var rows = node.ChildNodes.Where(n => n.Name == "tr");
			var headerRow = rows.First();
			var cells = headerRow.ChildNodes.Where(n => n.Name == "th");
			var columnNames = cells.Select(n => HtmlEntity.DeEntitize(n.FirstChild.InnerText)).ToList();

			var skipColumnIndices = skipColumns?.Select(c => columnNames.IndexOf(c)).Where(i => i >= 0).ToList() ?? new List<int>();


			var parsedRows = rows.Skip(1)
				.Select(r => r.ChildNodes.Where(n => n.Name == "td").ToList())
				.Select(row => row.Where((r, i) => !skipColumnIndices.Contains(i))
					.Select(c => {
						var hasLink = c.ChildNodes.SingleOrDefault(o => o.Name == "a");
						return hasLink != null ? hasLink.GetAttributeValue("href", "") : HtmlEntity.DeEntitize(c.InnerText);
				}).ToList()).ToList();


			return parsedRows.Select(r => {
				try
				{
					return deserializeRow(r);
				}
				catch (Exception ex)
				{
					throw new Exception($"Error deserializing row {string.Join("\t", r)}", ex);
				}
			}).ToList();
		}
		public static List<T> ParseDocument<T>(string html, Func<List<string>, T> deserializeRow, IEnumerable<string> skipColumns = null)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			return ParseDocument(doc, deserializeRow, skipColumns);
		}

		public List<TRow> ReadAll(string folder)
		{
			var files = new DirectoryInfo(folder).GetFiles(string.Format(FilenamePattern, "*")).OrderByDescending(o => o.Name);
			return files.SelectMany(file => {
					try
					{
						return Parse(File.ReadAllText(file.FullName));
					}
					catch (Exception ex)
					{
						throw new FormatException($"{ex.Message} for '{file.FullName}'", ex);
					}
				}).ToList();
		}
		public async IAsyncEnumerable<List<TRow>> ReadAllAsync(string folder)
		{
			var files = new DirectoryInfo(folder).GetFiles(string.Format(FilenamePattern, "*")).OrderByDescending(o => o.Name);
			foreach (var file in files)
			{
				List<TRow> rows;
				try
				{
					rows = Parse(await File.ReadAllTextAsync(file.FullName));
				}
				catch (Exception ex)
				{
					throw new FormatException($"{ex.Message} for '{file.FullName}'", ex);
				}
				yield return rows;
			}
		}

		private static CultureInfo _defaultCulture;
		public static CultureInfo DefaultCulture
		{
			get
			{
				if (_defaultCulture == null)
				{
					_defaultCulture = new CultureInfo("sv-SE");
					_defaultCulture.NumberFormat.NegativeSign = "-"; //.net 5 changed this to char 8722 instead..
				}
				return _defaultCulture;
			}
		}

		public static decimal ParseDecimal(string value, CultureInfo culture = null)
		{
			culture ??= DefaultCulture;
			if (decimal.TryParse(value.Replace(" ", ""), NumberStyles.Any, culture.NumberFormat, out var result))
				return result;
			throw new FormatException($"Can't parse decimal {value}");
		}
	}
}
