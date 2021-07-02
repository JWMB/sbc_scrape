using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MediusFlowAPI.Tests
{
	public class TestUtils
	{
		public static string GetFolder(string folderName, string removeFromFolder = "bin")
		{
			var folder = Environment.CurrentDirectory;
			var bin = $"{Path.DirectorySeparatorChar}{removeFromFolder}{Path.DirectorySeparatorChar}";
			if (folder.Contains(bin))
				folder = folder.Remove(folder.LastIndexOf(bin));
			return Path.Combine(folder, folderName);
		}

		public static T LoadJsonFromFile<T>(string folder, string path) where T : JToken
		{
			path = Path.Combine(Path.IsPathFullyQualified(folder) ? folder : GetFolder(folder), path);
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

		public static JToken GetAnonymized(string folder, string path)
		{
			var jToken = TestUtils.LoadJsonFromFile<JToken>(folder, path);

			var valuesToReplace = new[] {
				"$..Supplier.Name", "$..AuthorizerName", "$..OnBehalfOfUserName", "$..Company.Name",
				"$..OrganizationNumber", "$..author", "$..CompanyId", "$..CompanyName" };
			valuesToReplace.ToList().ForEach(selector => {
				var found = jToken.SelectTokens(selector).OfType<JValue>().ToList();
				found.ForEach(o => {
					//Replace with anonymous value
					o.Value = o.Type == JTokenType.String
						? (object)$"Anon"
						: (object)12345;
				});
			});
			//ExternalSystemId

			return jToken;
		}

		public static void AnonymizeFilesInFolder(string folder, string filePattern)
		{
			var files = new DirectoryInfo(TestUtils.GetFolder(folder)).GetFiles(filePattern).ToList();
			files.ForEach(file => {
				var anon = GetAnonymized(folder, file.Name);
				File.WriteAllText(file.FullName, anon.ToString());
			});
		}


	}
}
