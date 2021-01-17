using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonTools
{
	public static class IEnumerableExtensions
	{
		public struct EnumerableDiff<T>
		{
			public EnumerableDiff(IEnumerable<T> onlyIn1, IEnumerable<T> onlyIn2, IEnumerable<T> intersection)
			{
				OnlyIn1 = onlyIn1;
				OnlyIn2 = onlyIn2;
				Intersection = intersection;
			}

			public IEnumerable<T> OnlyIn1 { get; }
			public IEnumerable<T> OnlyIn2 { get; }
			public IEnumerable<T> Intersection { get; }
		}
		public static EnumerableDiff<T> GetListDiffs<T>(this IEnumerable<T> e1, IEnumerable<T> e2)
		{
			var intersection = e1.Intersect(e2);
			return new EnumerableDiff<T>(e1.Except(intersection), e2.Except(intersection), intersection);
		}


		public struct EnumerableDiff<T1, T2>
		{
			public EnumerableDiff(IEnumerable<T1> onlyIn1, IEnumerable<T2> onlyIn2)
			{
				OnlyIn1 = onlyIn1;
				OnlyIn2 = onlyIn2;
			}

			public IEnumerable<T1> OnlyIn1 { get; }
			public IEnumerable<T2> OnlyIn2 { get; }
		}
		public static EnumerableDiff<T1, T2> GetListDiffs<T1, T2, V>(
			this IEnumerable<T1> e1, Func<T1, V> getter1,
			IEnumerable<T2> e2, Func<T2, V> getter2)
		{
			var vals2 = e2.Select(getter2).ToList();
			var onlyIn1 = new List<T1>();
			var e2Copy = new List<T2>(e2);
			foreach (var e in e1)
			{
				var val = getter1(e);
				var index = vals2.IndexOf(val);
				if (index >= 0)
				{
					vals2.RemoveAt(index);
					e2Copy.RemoveAt(index);
				}
				else
				{
					onlyIn1.Add(e);
				}
			}
			return new EnumerableDiff<T1, T2>(onlyIn1, e2Copy);
		}
	}

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
