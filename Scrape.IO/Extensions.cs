using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Scrape.IO
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
			FileStream? stream = null;
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

			if (path.StartsWith(Path.DirectorySeparatorChar.ToString()))
				path = Path.Combine(Environment.CurrentDirectory, path);
			//path = Path.GetFullPath(path.Substring(1), Environment.CurrentDirectory);

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
}
