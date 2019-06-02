using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Scrape.IO.Storage
{
	public interface IKeyValueStore
	{
		Task Post(string key, object obj);

		Task<object> Get(string key);

		Task<List<string>> GetAllKeys();

		Task Delete(string key);
	}
}
