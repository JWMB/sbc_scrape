﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SBCScan.REPL;
using System;
using System.Collections.Generic;
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
					new QuitCmd(),
					new WriteFileCmd(Environment.CurrentDirectory),
					new ReadFileCmd(Environment.CurrentDirectory),
					new InitCmd(main),
					new AddCommandsTestCmd(),
				};
				cmds.Add(new ListCmd(cmds));

				await Command.RunCommandLoop(cmds);
			}
		}
	}
}