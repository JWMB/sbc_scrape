using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonTools
{
	public class Combinatorics
	{
		public static List<T> GetFirstOrDefaultMatchingCombo<T>(IList<T> items, int numItems, Func<List<T>, bool> isMatch)
		{
			foreach (var combo in GenerateCombos(items, numItems))
			{
				if (isMatch(combo)) return combo;
			}
			return null;
		}

		public static IEnumerable<List<T>> GenerateCombos<T>(IList<T> items, int numItems)
		{
			if (numItems < 1)
			{
				yield return new List<T>();
			}
			else if (numItems == items.Count)
			{
				yield return items.ToList();
			}
			else
			{
				var indices = new List<int>();
				for (int i = 0; i < numItems; i++)
					indices.Add(i);
				var lastIndex = numItems - 1;
				while (true)
				{
					yield return indices.Select(i => items[i]).ToList();
					if (indices[lastIndex] < items.Count - 1)
						indices[lastIndex]++;
					else
					{
						var indexToMove = lastIndex;
						while (true) //Find which index to reset to:
						{
							indexToMove--;
							if (indexToMove < 0)
								break;
							var start = indices[indexToMove];
							if (start < indices[indexToMove + 1] - 1)
								break;
						}
						if (indexToMove < 0)
							break;
						var startX = indices[indexToMove];
						for (int i = indexToMove; i < numItems; i++)
							indices[i] = startX + i - indexToMove + 1;
						if (indices[0] == items.Count - numItems)
						{
							yield return indices.Select(i => items[i]).ToList();
							break;
						}
					}
				}
			}
		}
	}
}
