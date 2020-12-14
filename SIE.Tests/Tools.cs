using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIE.Tests
{
	class Tools
	{
		public static async Task<List<RootRecord>> ReadSIEFiles(IEnumerable<string>? files = null)
		{
			var sieDir = Path.Join(GetCurrentOrSolutionDirectory(), "sbc_scrape", "scraped", "SIE");
			if (files == null)
				files = new DirectoryInfo(sieDir).GetFiles("*.se").Select(o => o.Name);
			return await SBCExtensions.ReadSIEFiles(files.Select(file => Path.Combine(sieDir, file)));
		}

		public static string GetCurrentOrSolutionDirectory()
		{
			var sep = "\\" + Path.DirectorySeparatorChar;
			var rx = new System.Text.RegularExpressions.Regex($@".*(?={sep}[^{sep}]+{sep}bin)");
			var m = rx.Match(Directory.GetCurrentDirectory());
			return m.Success ? m.Value : Directory.GetCurrentDirectory();
		}
	}
}
