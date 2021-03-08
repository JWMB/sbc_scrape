using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
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

			var cultureInfo = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
			cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
			cultureInfo.NumberFormat.NumberGroupSeparator = "";
			var csvConf = new CsvHelper.Configuration.CsvConfiguration(cultureInfo) { Delimiter = "\t", };
			csvConf.TypeConverterCache.AddConverter<LocalDate>(new CustomConverter<LocalDate?> {
				FromObject = value => value == null ? "" : value.Value.AtMidnight().ToDateTimeUnspecified().ToString("yyyy-MM-dd")
			});

			using (var main = ActivatorUtilities.CreateInstance<Main>(startup.Services))
			{
				var cmds = new List<Command> {
					new CreateIndexCmd(main),
					new CreateGroupedCmd(main),
					new CreateHouseIndexCmd(main),
					new ReadSBCHtml(GlobalSettings.AppSettings.StorageFolderSbcHtml),
					new OCRImagesCmd(),
					new InitCmd(main),
					new JoinDataSources(GlobalSettings.AppSettings, main),
					new ConvertInvoiceImageFilenameCmd(main),
					new ObjectToFilenameAndObject(),
					new GetAccountsListCmd(main),

					new QuitCmd(),
					new CSVCmd(csvConf),
					new WriteFileCmd(outputFolder),
					new WriteFiles(outputFolder),
					new ReadFileCmd(outputFolder),
					new AddCommandsTestCmd(),
					};
				cmds.Add(new ListCmd(cmds));

				var REPLRunner = new Runner(cmds);
				var readConsole = new ReadConsoleByChar("> ", new List<string> { "l older", "l old" });
				await REPLRunner.RunREPL(readConsole, CancellationToken.None);
			}
		}
	}

	public class CustomConverter<T> : CsvHelper.TypeConversion.ITypeConverter
	{
		public Func<T, string>? FromObject { get; set; } = null;
		public Func<string, T>? FromString { get; set; } = null;

		public object ConvertFromString(string text, CsvHelper.IReaderRow row, CsvHelper.Configuration.MemberMapData memberMapData)
		{
			if (FromString == null) throw new NotImplementedException();
			return FromString(text);
		}
		public string ConvertToString(object value, CsvHelper.IWriterRow row, CsvHelper.Configuration.MemberMapData memberMapData)
		{
			if (FromObject == null) throw new NotImplementedException();
			return FromObject((T)value);
		}
	}

}
