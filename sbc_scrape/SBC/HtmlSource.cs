using HtmlAgilityPack;
using System;
using System.Collections.Generic;
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
	}

	public abstract class HtmlSource<TRow> : HtmlSource
	{
		public abstract List<TRow> Parse(string html);
		public override List<object> ParseObjects(string html) => Parse(html).Cast<object>().ToList();
		public override List<object> ReadAllObjects(string folder) => ReadAll(folder).Cast<object>().ToList();

		public static List<T> ParseDocument<T>(HtmlDocument doc, Func<List<string>, T> deserializeRow, IEnumerable<string> skipColumns = null)
		{
			var node = doc.DocumentNode.SelectSingleNode("//table[@class='portal-table']");
			if (node == null)
				throw new FormatException("Couldn't find table node");
			node = node.ChildNodes.FirstOrDefault(n => n.Name == "tbody") ?? node; //Some variants have no tbody, but tr directly under

			var rows = node.ChildNodes.Where(n => n.Name == "tr");
			var headerRow = rows.First();
			var cells = headerRow.ChildNodes.Where(n => n.Name == "th");
			var columnNames = cells.Select(n => HtmlEntity.DeEntitize(n.FirstChild.InnerText)).ToList();

			var skipColumnIndices = skipColumns?.Select(c => columnNames.IndexOf(c)).Where(i => i >= 0).ToList() ?? new List<int>();


			var parsedRows = rows.Skip(1).Select(r => r.ChildNodes.Where(n => n.Name == "td").ToList())
				.Select(row => row.Where((r, i) => !skipColumnIndices.Contains(i)).Select(c => {
					return c.FirstChild.Name == "a" ? c.FirstChild.GetAttributeValue("href", "") : HtmlEntity.DeEntitize(c.InnerText);
				}).ToList()).ToList();

			return parsedRows.Select(r => deserializeRow(r)).ToList();
		}
		public static List<T> ParseDocument<T>(string html, Func<List<string>, T> deserializeRow, IEnumerable<string> skipColumns = null)
		{
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			return ParseDocument(doc, deserializeRow, skipColumns);
		}

		public List<TRow> ReadAll(string folder)
		{
			var files = new System.IO.DirectoryInfo(folder).GetFiles(string.Format(FilenamePattern, "*")).OrderByDescending(o => o.Name);
			return files.SelectMany(file => {
					try
					{
						return Parse(System.IO.File.ReadAllText(file.FullName));
					}
					catch (Exception ex)
					{
						throw new FormatException($"{ex.Message} for '{file.FullName}'", ex);
					}
				}).ToList();
		}
		public async IAsyncEnumerable<List<TRow>> ReadAllAsync(string folder)
		{
			var files = new System.IO.DirectoryInfo(folder).GetFiles(string.Format(FilenamePattern, "*")).OrderByDescending(o => o.Name);
			foreach (var file in files)
			{
				List<TRow> rows;
				try
				{
					rows = Parse(await System.IO.File.ReadAllTextAsync(file.FullName));
				}
				catch (Exception ex)
				{
					throw new FormatException($"{ex.Message} for '{file.FullName}'", ex);
				}
				yield return rows;
			}
		}

	}
}
