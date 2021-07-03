using System;
using System.Collections.Generic;
using System.Linq;

namespace CommonTools
{
	public class Set<TA, TB, TCompare>
	{
		public List<(TA A, TB B)> OneA_To_OneB => joined.Values
			.Where(o => o.Inner.Count == 1 && o.Outer.Count == 1)
			.Select(o => (o.Inner.Single(), o.Outer.Single()))
			.ToList();

		public List<(TA A, List<TB> Bs)> OneA_To_NoneOrAnyBs => joined
			.Where(o => o.Value.Inner.Count == 1)
			.Select(o => (o.Value.Inner.Single(), o.Value.Outer))
			.ToList();

		public List<(TA A, List<TB> Bs)> OneA_To_AnyBs =>
			OneA_To_NoneOrAnyBs
			.Where(o => o.Item2.Any())
			.ToList();

		public List<(TA A, List<TB> Bs)> OneA_To_ManyBs =>
			OneA_To_NoneOrAnyBs
			.Where(o => o.Item2.Count > 1)
			.ToList();

		public List<(List<TA> As, List<TB> Bs)> ManyA_To_ManyBs => joined.Values
			.Where(o => o.Inner.Count > 1 && o.Outer.Count > 1)
			.Select(o => (o.Inner, o.Outer))
			.ToList();

		public List<(List<TA> As, TB B)> ManyA_To_OneB => joined.Values
			.Where(o => o.Inner.Count > 1 && o.Outer.Count == 1)
			.Select(o => (o.Inner, o.Outer.Single()))
			.ToList();

		public List<TA> OnlyInA => joined.Values
			.Where(o => o.Outer.Any() == false)
			.SelectMany(o => o.Inner)
			.ToList();

		public List<TB> OnlyInB => outerByKey
			.Where(o => !joined.ContainsKey(o.Key))
			.SelectMany(o => o.Value).ToList();

		private Dictionary<TCompare, Joined> joined;
		private Dictionary<TCompare, List<TB>> outerByKey;

		public Dictionary<TCompare, Joined> JoinedByKey => joined;

		public class Joined
		{
			public List<TA> Inner { get; set; }
			public List<TB> Outer { get; set; }
		}

		public static Set<TA, TB, TCompare> Create(IEnumerable<TA> a, IEnumerable<TB> b,
			Func<TA, TCompare> keyA, Func<TB, TCompare> keyB)
		{
			var innerByKey = a.GroupBy(o => keyA(o)).ToDictionary(o => o.Key, o => o.ToList());
			var outerByKey = b.GroupBy(o => keyB(o)).ToDictionary(o => o.Key, o => o.ToList());
			var joined = innerByKey.Select(o => {
				var found = outerByKey.GetValueOrDefault(o.Key, new List<TB>());
				return new { Key = o.Key, Inner = o.Value, Outer = found };
			}).ToDictionary(o => o.Key, o => new Joined { Inner = o.Inner, Outer = o.Outer });

			var tmp = new Set<TA, TB, TCompare>();
			tmp.joined = joined;
			tmp.outerByKey = outerByKey;
			return tmp;
		}
	}
}
