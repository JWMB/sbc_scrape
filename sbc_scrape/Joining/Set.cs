//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace sbc_scrape.Joining
//{
//	public class Set<TA, TB, TCompare>
//	{
//		public List<(TA A, TB B)> OneA_To_OneB => joined.Values
//			.Where(o => o.Inner.Count == 1 && o.Outer.Count == 1)
//			.Select(o => (o.Inner.Single(), o.Outer.Single()))
//			//.Where(o => o.Count() == 1 && o.Single().Inner.Count == 1 && o.Single().Outer?.Count == 1)
//			//.Select(o => (o.Single().Inner.Single(), o.Single().Outer.Single()))
//			//.Where(o => o.Count() == 1 && o.Single().Outer?.Count == 1)
//			//.Select(o => (o.Single().Inner, o.Single().Outer.Single() ))
//			.ToList();

//		public List<(TA A, List<TB> Bs)> OneA_To_NoneOrAnyBs => joined
//			.Where(o => o.Value.Inner.Count == 1)
//			.Select(o => (o.Value.Inner.Single(), o.Value.Outer))
//			//.Where(o => o.Value.Count() == 1 && o.Value.Single().Inner.Count == 1)
//			//.Select(o => (o.Value.Single().Inner.Single(), outerByKey.GetValueOrDefault(o.Key, new List<TB>())))
//			//.Where(o => o.Count() == 1)
//			//.Select(o => (o.Single().Inner, outerByKey.GetValueOrDefault(o.Single().Key, new List<TB>())))
//			.ToList();

//		public List<(TA A, List<TB> Bs)> OneA_To_AnyBs =>
//			OneA_To_NoneOrAnyBs
//			//joined.Values
//			//.Where(o => o.Count() == 1)
//			//.Select(o => (o.Single().Inner, outerByKey.GetValueOrDefault(o.Single().Key, new List<TB>())))
//			.Where(o => o.Item2.Any())
//			.ToList();

//		public List<(TA A, List<TB> Bs)> OneA_To_ManyBs =>
//			OneA_To_NoneOrAnyBs
//			//joined.Values
//			//.Where(o => o.Count() == 1)
//			//.Select(o => (o.Single().Inner, outerByKey.GetValueOrDefault(o.Single().Key, new List<TB>())))
//			.Where(o => o.Item2.Count > 1)
//			.ToList();

//		public List<(List<TA> As, List<TB> Bs)> ManyA_To_ManyBs => joined.Values
//			.Where(o => o.Inner.Count > 1 && o.Outer.Count > 1)
//			.Select(o => (o.Inner, o.Outer))
//			//.Where(o => o.Count() > 1 && o.Any(p => p.Outer?.Count > 1))
//			//.SelectMany(o => o.Select(p => (p.Inner, p.Outer)))
//			//.Where(o => o.Count() > 1 || o.Any(p => p.Outer?.Count > 1))
//			//.Select(o => (o.Select(p => p.Inner).ToList(), outerByKey.GetValueOrDefault(o.First().Key, new List<TB>()).ToList()))
//			//.Where(o => o.Item1.Count > 1 && o.Item2.Count > 1)
//			.ToList();

//		public List<(List<TA> As, TB B)> ManyA_To_OneB => joined.Values
//			.Where(o => o.Inner.Count > 1 && o.Outer.Count == 1)
//			.Select(o => (o.Inner, o.Outer.Single()))
//			//.Where(o => o.Count() > 1 && o.Any(p => p.Outer?.Count == 1))
//			//.SelectMany(o => o.Select(p => (p.Inner, p.Outer.Single())))
//			.ToList();

//		public List<TA> OnlyInA => joined.Values
//			.Where(o => o.Outer.Any() == false)
//			.SelectMany(o => o.Inner)
//			//.Where(o => o.Count() == 1 && o.Single().Outer?.Any() != true)
//			//.SelectMany(o => o.Single().Inner)
//			//.Where(o => o.Count() == 1 && o.Single().Outer?.Any() != true)
//			//.Select(o => o.Single().Inner)
//			.ToList();

//		public List<TB> OnlyInB => outerByKey
//			.Where(o => !joined.ContainsKey(o.Key))
//			.SelectMany(o => o.Value).ToList();

//		private Dictionary<TCompare, Joined> joined;
//		private Dictionary<TCompare, List<TB>> outerByKey;

//		public Dictionary<TCompare, Joined> JoinedByKey => joined;

//		public class Joined
//		{
//			//public TCompare Key { get; set; }
//			public List<TA> Inner { get; set; }
//			public List<TB> Outer { get; set; }
//		}

//		public static Set<TA, TB, TCompare> Create(IEnumerable<TA> a, IEnumerable<TB> b,
//			Func<TA, TCompare> keyA, Func<TB, TCompare> keyB)
//		{
//			var innerByKey = a.GroupBy(o => keyA(o)).ToDictionary(o => o.Key, o => o.ToList());
//			var outerByKey = b.GroupBy(o => keyB(o)).ToDictionary(o => o.Key, o => o.ToList());
//			var joined = innerByKey.Select(o => {
//				var found = outerByKey.GetValueOrDefault(o.Key, new List<TB>());
//				return new { Key = o.Key, Inner = o.Value, Outer = found };
//			})
//				//var joined = a.Select(o => {
//				//	var key = keyA(o);
//				//	var found = outerByKey.GetValueOrDefault(key, null);
//				//	return new Join { Key = key, Inner = o, Outer = found };
//				//})
//				//.GroupBy(o => o.Key).ToDictionary(o => o.Key, o => o.Select(p => new Joined { Inner = p.Inner, Outer = p.Outer }).ToList());
//				.ToDictionary(o => o.Key, o => new Joined { Inner = o.Inner, Outer = o.Outer });
//			var tmp = new Set<TA, TB, TCompare>();
//			tmp.joined = joined;
//			tmp.outerByKey = outerByKey;
//			return tmp;
//		}
//	}
//}
