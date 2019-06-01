using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.Storage
{
	public interface IDocument
	{

	}
	public interface IDocumentStore
	{
		Task Post(string key, object obj);

		Task<object> Get(string key);

		Task<List<string>> GetAllKeys();

		Task Delete(string key);
	}
}
