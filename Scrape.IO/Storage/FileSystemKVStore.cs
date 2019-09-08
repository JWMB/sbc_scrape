using Newtonsoft.Json;
using Scrape.IO;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scrape.IO.Storage
{
	public class FileSystemKVStore : IKeyValueStore
	{
		protected readonly string rootPath;
		readonly string extension;

		public FileSystemKVStore(string rootPath, string extension = ".json")
		{
			this.rootPath = PathExtensions.Parse(rootPath);
			if (!Directory.Exists(rootPath))
				Directory.CreateDirectory(rootPath);
			this.extension = extension;
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
			//TODO: read as byte[]?
			return File.ReadAllText(KeyToPath(key));
			//TODO: await File.ReadAllTextAsync(KeyToPath(key));
		}
		public async Task<T> Get<T>(string key)
		{
			var content = File.ReadAllText(KeyToPath(key)); //await File.ReadAllTextAsync(KeyToPath(key));
			return JsonConvert.DeserializeObject<T>(content);
		}

		public async Task Post(string key, object obj)
		{
			if (obj is byte[] bytes)
				File.WriteAllBytes(KeyToPath(key), bytes);
			else
				File.WriteAllText(KeyToPath(key), JsonConvert.SerializeObject(obj, Formatting.Indented));
			//TODO: WriteAllTextAsync(KeyToPath(key), JsonConvert.SerializeObject(obj, Formatting.Indented));
		}

		public async Task Delete(string key)
		{
			File.Delete(KeyToPath(key));
		}

		public async Task<List<string>> GetAllKeys()
		{
			var files = new DirectoryInfo(rootPath).GetFiles();
			return files.Where(o => o.Name.EndsWith(extension)).Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList();
		}
	}
}
