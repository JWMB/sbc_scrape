using MediusFlowAPI.Models.Task;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SBCScan.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SBCScan
{
	class Startup
	{
		public IConfigurationRoot Configuration { get; private set; }
		public ServiceProvider Services { get; private set; }

		public Startup(string[] args)
		{
			var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
			var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";
			//Determines the working environment as IHostingEnvironment is unavailable in a console app

			var builder = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
			//.AddEnvironmentVariables();
			//.AddCommandLine(args)

			if (isDevelopment)
			{
				builder.AddUserSecrets<AppSettings>();
			}
			Configuration = builder.Build();

			var services = new ServiceCollection();
			ConfigureServices(services);
			Services = services.BuildServiceProvider();
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.Configure<AppSettings>(Configuration.GetSection(nameof(AppSettings)));

			var intermediary = services.BuildServiceProvider();
			var settings = intermediary.GetService<IOptions<AppSettings>>();

			services.AddLogging(configure => configure.AddConsole());
			services.AddOptions();
			services.AddScoped<IDocumentStore, SbcFileStorage>(sp => new SbcFileStorage(settings.Value.StorageFolderRoot));
		}
	}
}
