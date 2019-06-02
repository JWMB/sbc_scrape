using MediusFlowAPI;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBCScan
{
	public class Fetcher : IFetcher
	{
		private readonly RemoteWebDriver driver;
		private readonly string downloadFolderToCheck;

		public Fetcher(RemoteWebDriver driver, string downloadFolderToCheck)
		{
			this.driver = driver;
			this.downloadFolderToCheck = downloadFolderToCheck;
		}

		private async Task<string> GetInNewTab(string url, string expectedFilename)
		{
			var originalHandle = driver.CurrentWindowHandle;
			var body = driver.FindElement(By.TagName("body"));
			body.SendKeys(Keys.Control + 't');
			var newHandle = driver.CurrentWindowHandle;

			var gotNewTab = originalHandle != newHandle;
			driver.Navigate().GoToUrl(url);
			var file = new FileInfo(Path.Combine(downloadFolderToCheck, expectedFilename));
			var timeout = DateTime.Now.AddSeconds(3);
			while (DateTime.Now < timeout)
			{
				if (file.Exists)
					break;
				await Task.Delay(100);
			}
			if (!file.Exists)
				throw new Exception("ddd");

			var lastLength = 0L;
			while (true)
			{
				if (file.Length == lastLength)
				{
					if (!file.IsLocked())
						break;
				}
				lastLength = file.Length;
				await Task.Delay(100);
			}

			if (gotNewTab)
				driver.Close();

			return file.FullName;
		}
		public async Task<string> DownloadFile(string url, FetchConfig config = null, string overrideFilenameHeader = null)
		{
			config = config ?? new FetchConfig
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
				var contentDisposition = response.Headers?.GetOrDefault("content-disposition", null);
				if (contentDisposition != null)
				{
					var m = rxFileName.Match(contentDisposition);
					if (m.Success)
						filename = m.Groups["filename"].Value;
				}
				var path = Path.Combine(downloadFolderToCheck, overrideFilenameHeader ?? filename);
				await File.WriteAllBytesAsync(path, bytes);
				return path;
			}
			return response.Body.ToString();
		}
		static Regex rxFileName = new Regex(@"filename=""(?<filename>[\w-]+(\.\w+))");
		public async Task<FetchResponse> Fetch(string url, FetchConfig config = null)
		{
			config = config ?? new FetchConfig();
			var isBinary = false;
			if (config.Headers == null || !config.Headers.TryGetValue("accept", out string acceptHeader))
				acceptHeader = "";

			var additionalScripts = "";
			var initialResultConversionFunction = "";
			var bodyConversion = "body";
			string responseParsing;
			if (acceptHeader.Contains("application/json"))
				initialResultConversionFunction = "json";
			else if (acceptHeader.Contains("application/octet-stream") || acceptHeader.Contains("image/"))
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

			var body = config.Body == null ? null : JsonConvert.SerializeObject(config.Body);

			string NullOrQuoted(string str) => str == null ? "null" : $"'{str}'";

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
			if (response is Dictionary<string, object> dict)
			{
				var headers = new Dictionary<string, string>();
				object evaluatedBody = null;
				var headersRaw = dict.GetOrDefault("headers", null) as System.Collections.ObjectModel.ReadOnlyCollection<object>;
				if (headersRaw != null)
				{
					try
					{
						headers = headersRaw.Cast<System.Collections.ObjectModel.ReadOnlyCollection<object>>()
							.ToDictionary(k => k[0].ToString(), k => k[1].ToString());
					}
					catch (Exception ex)
					{ }
				}

				if (isBinary)
				{
					var isImage = true;
					evaluatedBody = Convert.FromBase64String(isImage ? FixBase64ForImage((string)dict["body"]) : (string)dict["body"]);

					string FixBase64ForImage(string Image)
					{
						var sbText = new StringBuilder(Image, Image.Length);
						sbText.Replace("\r\n", string.Empty);
						sbText.Replace(" ", string.Empty);
						return sbText.ToString();
					}
				}
				if (!dict.ContainsKey("body"))
				{ }
				return new FetchResponse {
					Body = evaluatedBody != null ? evaluatedBody : dict.GetOrDefault("body", null),
					Status = dict.GetOrDefault("status", null).ToString(),
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
