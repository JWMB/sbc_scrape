using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan
{
	public static class AsyncEnumerableExtensions
	{
		public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> asyncEnum)
		{
			var result = new List<T>();
			await foreach (var o in asyncEnum)
				result.Add(o);
			return result;
		}
	}
}
