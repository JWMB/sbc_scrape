using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using Scrape.IO;
using Scrape.IO.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scrape.IO.Selenium
{
	public class Fetcher : IFetcher
	{
		private readonly RemoteWebDriver driver;

		public Fetcher(RemoteWebDriver driver, IKeyValueStore store)
		{
			this.driver = driver;
			Store = store;
		}

		public IKeyValueStore Store { get; }

		public async Task<string?> DownloadFile(string url, FetchConfig? config = null, string? overrideFilenameHeader = null)
		{
			config ??= new FetchConfig
			{
				Headers = new Dictionary<string, string> {
					{ "accept", "image/png,image/webp,image/apng,*/*;q=0.8" },
					{ "accept-encoding", "gzip, deflate, br"},
					{ "cache-control", "no-cache" }
				},
				Credentials = CredentialsMode.Omit,
				Mode = CorsMode.NoCors,
			};

			var response = await Fetch(url, config);
			if (response.Body is byte[] bytes)
			{
				var filename = $"NOTSET_{Guid.NewGuid()}";
				var contentDisposition = response.Headers?.GetOrDefault("content-disposition", string.Empty);
				if (contentDisposition != string.Empty)
				{
					var m = rxFileName.Match(contentDisposition);
					if (m.Success)
						filename = m.Groups["filename"].Value;
				}
				filename = overrideFilenameHeader ?? filename;
				await Store.Post(filename, bytes);
				return filename;
			}
			return response.Body?.ToString();
		}

		static readonly Regex rxFileName = new Regex(@"filename=""(?<filename>[\w-]+(\.\w+))");
		public async Task<FetchResponse> Fetch(string url, FetchConfig? config = null)
		{
			config ??= new FetchConfig();
			var isBinary = false;
			if (config.Headers == null || !config.Headers.TryGetValue("accept", out string? acceptHeader))
				acceptHeader = "";

			var additionalScripts = "";
			var initialResultConversionFunction = "";
			var bodyConversion = "body";
			string responseParsing;
			if (acceptHeader.Contains("application/json"))
				initialResultConversionFunction = "json";
			else if (acceptHeader.Contains("application/octet-stream") || acceptHeader.Contains("image/") || acceptHeader.Contains("application/pdf"))
			{
				isBinary = true;
				initialResultConversionFunction = "arrayBuffer";
				bodyConversion = "btoa(new Uint8Array(body).reduce((data,byte)=>(data.push(String.fromCharCode(byte)),data),[]).join(''))";
//				responseParsing = $@".then(response => response.arrayBuffer())
//.then(response => btoa(new Uint8Array(response).reduce((data,byte)=>(data.push(String.fromCharCode(byte)),data),[]).join('')))";
			}
			else
				initialResultConversionFunction = "text";

			responseParsing = $@".then(res => res.{initialResultConversionFunction}().then(body => ({{
headers: Array.from(res.headers.entries()), 
status: res.status, 
body: {bodyConversion}
}})))
";
			if (config.Method.Value == MethodMode.Get.Value && config.Body != null)
			{
				var parms = config.Body.GetType().GetProperties().Select(p => new { Key = p.Name, Value = p.GetValue(config.Body, new object[] { }) });
				if (parms.Any())
					url += (url.Contains("?") ? "&" : "?") + string.Join("&", parms.Select(o => $"{System.Net.WebUtility.UrlEncode(o.Key)}={System.Net.WebUtility.UrlEncode(o.Value?.ToString() ?? "")}"));
				config.Body = null;
			}
			var body = config.Body == null ? null : JsonConvert.SerializeObject(config.Body);

			static string NullOrQuoted(string? str) => str == null ? "null" : $"'{str}'";

			var script = $@"{additionalScripts}
fetch('{url}',
{{ 
	method: '{config.Method}',
	mode: '{config.Mode}',
	headers: {{ {(config.Headers == null ? "" : string.Join(", ", config.Headers.Select(item => $"'{item.Key}': '{item.Value}'")))} }},
	body: {NullOrQuoted(body)},
	credentials: '{config.Credentials}',
}})
{responseParsing}
.then(res => {{ console.log(res); }})
.catch(error => {{ console.warn(error); console.log(error); }});
";

			var forExec = "let callback = arguments[arguments.length - 1];" + script.Replace("console.log", "callback");
			var response = await Task.Run(() =>
			{
				return ((IJavaScriptExecutor)driver).ExecuteAsyncScript(forExec);
			});
			if (response is Dictionary<string, object?> dict)
			{
				var headers = new Dictionary<string, string>();
				object? evaluatedBody = null;
				var headersRaw = dict.GetOrDefault("headers", null) as System.Collections.ObjectModel.ReadOnlyCollection<object>;
				if (headersRaw != null)
				{
					try
					{
						headers = headersRaw?.Cast<System.Collections.ObjectModel.ReadOnlyCollection<object>>()
							.ToDictionary(k => k[0].ToString() ?? "", k => k[1].ToString() ?? "") ?? new Dictionary<string, string>();
					}
					catch (Exception ex)
					{
						throw ex;
					}
				}

				var responseBody = dict.GetValueOrDefault("body", null);
				if (responseBody != null && isBinary)
				{
					var isImage = true;
					evaluatedBody = Convert.FromBase64String(isImage ? FixBase64ForImage((string)responseBody) : (string)responseBody);

					static string FixBase64ForImage(string Image)
					{
						var sbText = new StringBuilder(Image, Image.Length);
						sbText.Replace("\r\n", string.Empty);
						sbText.Replace(" ", string.Empty);
						return sbText.ToString();
					}
				}

				return new FetchResponse {
					Body = evaluatedBody ?? responseBody,
					Status = dict.GetOrDefault("status", null)?.ToString() ?? "N/A",
					Headers = headers
				};
			}
			else if (response is string str)
			{
			}
			return new FetchResponse { Body = response, Headers = null, Status = null };
		}
	}
}
