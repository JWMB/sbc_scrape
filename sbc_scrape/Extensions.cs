using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SBCScan
{
	public static class CollectionExtensions
	{
		public static V GetOrDefault<K, V>(this Dictionary<K, V> dict, K key, V defaultValue)
		{
			if (dict.TryGetValue(key, out var value))
				return value;
			return defaultValue;
		}
	}
	public static class FileInfoExtensions
	{
		public static bool IsLocked(this FileInfo file)
		{
			FileStream stream = null;
			try
			{
				stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
			}
			catch (IOException)
			{
				return true;
			}
			finally
			{
				if (stream != null)
					stream.Close();
			}

			return false;
		}
	}
	public static class PathExtensions
	{
		static Regex rx = new Regex(@"%(\w+)%");
		public static string Parse(string path)
		{
			var other = Path.DirectorySeparatorChar == '\\' ? '/' : '\\';
			path = path.Replace(other, Path.DirectorySeparatorChar);

			if (path.StartsWith(Path.DirectorySeparatorChar))
				path = Path.GetFullPath(path.Substring(1), Environment.CurrentDirectory); // Path.Combine(Environment.CurrentDirectory, path);

			string Substitute(string id)
			{
				if (Enum.TryParse<Environment.SpecialFolder>(id, true, out var found))
					return Environment.GetFolderPath(found);
				if (id.ToLower() == "ProjectOrCurrent".ToLower())
				{
					if (System.Diagnostics.Debugger.IsAttached)
						return Environment.CurrentDirectory.Remove(Environment.CurrentDirectory.LastIndexOf($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
					return Environment.CurrentDirectory;
				}
				return id;
			}
			return rx.Replace(path, (m) => Substitute(m.Groups[1].Value));
		}
	}

	public static class WebDriverExtensions
	{
		public static Func<IWebDriver, bool> ElementIsPresent(By by)
		{
			return driver =>
			{
				try { driver.FindElement(by); return true; }
				catch { return false; }
			};
		}

		public static Func<IWebDriver, bool> ElementIsVisible(IWebElement element)
		{
			return driver =>
			{
				try { return element.Displayed; }
				catch { return false; }
			};
		}
		public static void WaitUntilDocumentReady(this IWebDriver driver, int timeoutSeconds = 60)
		{
			new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds)).Until(
	d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
		}
	}
}
