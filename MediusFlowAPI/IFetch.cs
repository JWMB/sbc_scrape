using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediusFlowAPI
{
	public abstract class ModeBase
	{
		protected ModeBase(string value) { Value = value; }
		public string Value { get; set; }
		public override string ToString() => Value;
	}

	public class RedirectMode : ModeBase
	{
		private RedirectMode(string value) : base(value) { }

		public static RedirectMode Follow { get => new RedirectMode("follow"); }
		public static RedirectMode Manual { get => new RedirectMode("manual"); }
		public static RedirectMode Error { get => new RedirectMode("error"); }
	}

	public class CorsMode : ModeBase
	{
		private CorsMode(string value) : base(value) { }
		public static CorsMode SameOrigin { get => new CorsMode("same-origin"); }
		public static CorsMode Cors { get => new CorsMode("cors"); }
		public static CorsMode NoCors { get => new CorsMode("no-cors"); }
	}

	public class ReferrerMode : ModeBase
	{
		private ReferrerMode(string value) : base(value) { }
		public static ReferrerMode Client { get => new ReferrerMode("client"); }
		public static ReferrerMode NoReferrer { get => new ReferrerMode("no-referrer"); }
	}

	public class CacheMode : ModeBase
	{
		private CacheMode(string value) : base(value) { }
		public static CacheMode Default { get => new CacheMode("default"); }
		public static CacheMode NoCache { get => new CacheMode("no-cache"); }
		public static CacheMode Reload { get => new CacheMode("reload"); }
		public static CacheMode ForceCache { get => new CacheMode("force-cache"); }
		public static CacheMode OnlyIfCached { get => new CacheMode("only-if-cached"); }
	}

	public class CredentialsMode : ModeBase
	{
		private CredentialsMode(string value) : base(value) { }
		public static CredentialsMode SameOrigin { get => new CredentialsMode("same-origin"); }
		public static CredentialsMode Include { get => new CredentialsMode("include"); }
		public static CredentialsMode Omit { get => new CredentialsMode("omit"); }
	}

	public class MethodMode : ModeBase
	{
		private MethodMode(string value) : base(value) { }
		public static MethodMode Post { get => new MethodMode("POST"); }
		public static MethodMode Get { get => new MethodMode("GET"); }
		public static MethodMode Put { get => new MethodMode("PUT"); }
		public static MethodMode Delete { get => new MethodMode("DELETE"); }
		public static MethodMode Options { get => new MethodMode("OPTIONS"); }
		public static MethodMode Head { get => new MethodMode("HEAD"); }
		public static MethodMode Parse(string mode)
		{
			return new MethodMode[] { Post, Get, Put, Delete, Options, Head }.FirstOrDefault(m => m.Value.ToLower() == mode.ToLower());
		}
	}

	public class ReferrerPolicyMode : ModeBase
	{
		private ReferrerPolicyMode(string value) : base(value) { }
		public static ReferrerPolicyMode NoReferrer { get => new ReferrerPolicyMode("no-referrer"); }
		public static ReferrerPolicyMode NoReferrerWhenDowngrade { get => new ReferrerPolicyMode("no-referrer-when-downgrade"); }
		public static ReferrerPolicyMode Origin { get => new ReferrerPolicyMode("origin"); }
		public static ReferrerPolicyMode OriginWhenCrossOrigin { get => new ReferrerPolicyMode("origin-when-cross-origin"); }
		public static ReferrerPolicyMode SameOrigin { get => new ReferrerPolicyMode("same-origin"); }
		public static ReferrerPolicyMode StrictOrigin { get => new ReferrerPolicyMode("strict-origin"); }
		public static ReferrerPolicyMode StrictOriginWhenCrossOrigin { get => new ReferrerPolicyMode("strict-origin-when-cross-origin"); }
		public static ReferrerPolicyMode UnsafeUrl { get => new ReferrerPolicyMode("unsafe-url"); }
	}

	public class FetchConfig
	{
		public MethodMode Method { get; set; } = MethodMode.Get;

		public object Body { get; set; }

		public Dictionary<string, string> Headers { get; set; }

		public CorsMode Mode { get; set; } = CorsMode.SameOrigin;

		public ReferrerPolicyMode ReferrerPolicy { get; set; } = ReferrerPolicyMode.NoReferrerWhenDowngrade;

		public CredentialsMode Credentials { get; set; } = CredentialsMode.SameOrigin;
	}

	public class FetchResponse
	{
		public Dictionary<string, string> Headers { get; set; }
		public object Body { get; set; }
		public string Status { get; set; }
	}

	public interface IFetcher
	{
		Task<FetchResponse> Fetch(string url, FetchConfig config = null);
		Task<string> DownloadFile(string url, FetchConfig config = null, string overrideFilenameHeader = null);
	}
}
