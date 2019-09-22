using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Scrape.Main.Tests
{
	[TestClass]
	public class UnitTest1
	{
		private string GetOutputFolder()
		{
			var folder = Environment.CurrentDirectory;
			var bin = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
			if (folder.Contains(bin))
				folder = folder.Remove(folder.LastIndexOf(bin));
			return Path.Combine(folder, "scraped");
		}

		private T LoadJsonFromFile<T>(string path) where T : JToken
		{
			path = Path.Combine(GetOutputFolder(), path);
			if (!File.Exists(path))
				throw new FileNotFoundException(path);
			var json = File.ReadAllText(path);
			if (json.Length == 0)
				throw new FileLoadException($"File empty: {path}");
			var token = JToken.Parse(json);
			if (token is T)
				return token as T;
			throw new Exception($"JSON in '{path}' is not a {typeof(T).Name}");
		}

		[TestMethod]
		public void TestMethod1()
		{
		}
	}
}
