using Newtonsoft.Json;
using Scrape.IO;
using System;
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
			//return File.ReadAllText(KeyToPath(key));
			return await File.ReadAllTextAsync(KeyToPath(key));
		}
		public async Task<T> Get<T>(string key)
		{
			var content = await File.ReadAllTextAsync(KeyToPath(key));  //File.ReadAllText(KeyToPath(key)); //
			return JsonConvert.DeserializeObject<T>(content);
		}

		public async Task Post(string key, object obj)
		{
			if (obj is byte[] bytes)
				await File.WriteAllBytesAsync(KeyToPath(key), bytes);
			else
				await File.WriteAllTextAsync(KeyToPath(key), JsonConvert.SerializeObject(obj, Formatting.Indented));
			//TODO: WriteAllTextAsync(KeyToPath(key), JsonConvert.SerializeObject(obj, Formatting.Indented));
		}

		public Task Delete(string key)
		{
			File.Delete(KeyToPath(key));
			return Task.FromResult(0);
		}

		public Task<List<string>> GetAllKeys()
		{
			var files = new DirectoryInfo(rootPath).GetFiles();
			return Task.FromResult(files.Where(o => o.Name.EndsWith(extension)).Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList());
		}
	}

	public class InMemoryKVStore : IKeyValueStore
	{
		protected readonly Dictionary<string, object> store = new Dictionary<string, object>();

		private Task Taskify(Action act)
		{
			act();
			return Task.FromResult(0);
		}
		public Task Delete(string key) => Taskify(() => store.Remove(key));
		public Task<object> Get(string key) => Task.FromResult(store.GetValueOrDefault(key, null));
		public Task<List<string>> GetAllKeys() => Task.FromResult(store.Keys.ToList());
		public Task Post(string key, object obj) => Taskify(() => store[key] = obj);
	}

	public interface IKeyValueStoreOfT<T>
	{
		Task Post(string key, T obj);

		Task<T> Get(string key);

		Task<List<string>> GetAllKeys();

		Task Delete(string key);
	}

	public class KeyValueStoreOfT<T> : IKeyValueStoreOfT<T>
	{
		private readonly IKeyValueStore store;
		private readonly Func<T, string> convertToStore;
		private readonly Func<string, T> convertFromStore;

		public KeyValueStoreOfT(IKeyValueStore underlyingStorage, Func<T, string> convertToStore, Func<string, T> convertFromStore)
		{
			store = underlyingStorage;
			this.convertFromStore = convertFromStore;
			this.convertToStore = convertToStore;
		}

		public Task Post(string key, T obj) => store.Post(key, convertToStore(obj));

		public async Task<T> Get(string key) => convertFromStore((await store.Get(key)).ToString());

		public Task<List<string>> GetAllKeys() => store.GetAllKeys();

		public async Task Delete(string key) => await store.Delete(key);
	}
}
