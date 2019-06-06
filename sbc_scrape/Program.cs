using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using REPL;
using SBCScan.REPL;
using Scrape.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SBCScan
{
	class Program
	{
		public static IConfigurationRoot Configuration { get; set; }

		private static async Task Main(string[] args)
		{
			var startup = new Startup(args);

			using (var main = ActivatorUtilities.CreateInstance<Main>(startup.Services))
			{
				var cmds = new List<Command> {
					new CreateIndexCmd(main),
					new CreateGroupedCmd(main),
					new CreateHouseIndexCmd(main),
					new ReadSBCInvoices(Path.Combine(GlobalSettings.AppSettings.StorageFolderRootResolved, "sbc_fakturaparm")),
					new QuitCmd(),
					new CSVCmd(),
					new WriteFileCmd(Environment.CurrentDirectory),
					new ReadFileCmd(Environment.CurrentDirectory),
					new InitCmd(main),
					new AddCommandsTestCmd(),
					};
				cmds.Add(new ListCmd(cmds));

				await Command.RunREPL(cmds);
			}
		}
	}
}
