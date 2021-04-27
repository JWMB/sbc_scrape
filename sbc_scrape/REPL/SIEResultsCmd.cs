using REPL;
using SBCScan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sbc_scrape.REPL
{
	class SIEResultsCmd : Command
	{
		private readonly AppSettings settings;

		public override string Help => "ResultRäkning";
		public override string Id => "rr";

		public SIEResultsCmd(AppSettings settings)
		{
			this.settings = settings;
		}

		public async Task<string> Evaluate(int startYear = -1, int endYear = -1)
		{
			if (startYear < 0) startYear = DateTime.Now.Year - 10;
			if (endYear < 0) endYear = DateTime.Now.Year - 1;
			var years = Enumerable.Range(startYear, endYear - startYear + 1);

			var sieFiles = new System.IO.DirectoryInfo(settings.StorageFolderSIE)
	.GetFiles($"output_*.se")
	.Where(o => years.Any(p => o.Name.Contains(p.ToString())))
	.Select(o => o.FullName);
			var sie = await SIE.SBCExtensions.ReadSIEFiles(sieFiles);
			var csv = SIE.Exports.ResultatRakningCsv(sie);
			return csv;
		}
	}
}
