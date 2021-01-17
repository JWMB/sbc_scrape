using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using REPL;
using SBCScan.REPL;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SBCScan
{
	class Program
	{
		public static IConfigurationRoot Configuration { get; set; }

		private static async Task Main(string[] args)
		{
			var startup = new Startup(args);

			var outputFolder = GlobalSettings.AppSettings.OutputFolder;
			if (!Directory.Exists(outputFolder))
				Directory.CreateDirectory(outputFolder);

			using (var main = ActivatorUtilities.CreateInstance<Main>(startup.Services))
			{
				var cmds = new List<Command> {
					new CreateIndexCmd(main),
					new CreateGroupedCmd(main),
					new CreateHouseIndexCmd(main),
					new ReadSBCHtml(GlobalSettings.AppSettings.StorageFolderSbcHtml),
					new OCRImagesCmd(),
					new InitCmd(main),
					new JoinDataSources(GlobalSettings.AppSettings.StorageFolderSbcHtml, main),
					new ConvertInvoiceImageFilenameCmd(main),
					new ObjectToFilenameAndObject(),
					new GetAccountsListCmd(main),

					new QuitCmd(),
					new CSVCmd(),
					new WriteFileCmd(outputFolder),
					new WriteFiles(outputFolder),
					new ReadFileCmd(outputFolder),
					new AddCommandsTestCmd(),
					};
				cmds.Add(new ListCmd(cmds));

				FetchSIE.GetExistingYears();
				var REPLRunner = new Runner(cmds);
				await REPLRunner.RunREPL(CancellationToken.None);
			}
		}
	}
}
