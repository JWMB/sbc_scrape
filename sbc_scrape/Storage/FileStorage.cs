using MediusFlowAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBCScan.Storage
{
	public class SbcFileStorage : IDocumentStore
	{
		protected readonly string rootPath;
		readonly string extension = ".json";

		public SbcFileStorage(string rootPath)
		{
			this.rootPath = PathExtensions.Parse(rootPath);
			if (!Directory.Exists(rootPath))
				Directory.CreateDirectory(rootPath);
		}

		protected string KeyToPath(string key)
		{
			return Path.Combine(rootPath, key) + extension;
		}
		protected string PathToKey(string path)
		{
			var key = path.Replace(rootPath, "");
			return key.Remove(key.LastIndexOf(extension));
		}

		public async Task<object> Get(string key)
		{
			return await File.ReadAllTextAsync(KeyToPath(key));
		}
		public async Task<T> Get<T>(string key)
		{
			var content = await File.ReadAllTextAsync(KeyToPath(key));
			return JsonConvert.DeserializeObject<T>(content);
		}

		public async Task Post(string key, object obj)
		{
			await File.WriteAllTextAsync(KeyToPath(key), JsonConvert.SerializeObject(obj, Formatting.Indented));
		}

		public async Task Delete(string key)
		{
			File.Delete(KeyToPath(key));
		}

		public async Task<List<string>> GetAllKeys()
		{
			var files = new DirectoryInfo(rootPath).GetFiles();
			return files.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList();
		}
	}
}
