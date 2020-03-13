using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Scrape.IO;

namespace sbc_scrape
{
	class OCR
	{
		public static string Run(string imageFile, IEnumerable<string> languages)
		{
			var pathToTesseract = SBCScan.GlobalSettings.AppSettings.PathToTesseract;
			if (!File.Exists(pathToTesseract))
				throw new FileNotFoundException($"Tesseract not found at '{pathToTesseract}'");

			var outfile = Path.Combine(Directory.GetCurrentDirectory(), $"ocr_tmp{DateTime.Now.Ticks % 100000}");
			var process = Process.Start(new ProcessStartInfo {
				FileName = pathToTesseract,
				Arguments = $"\"{imageFile}\" \"{outfile}\" -l {(string.Join("+", languages))}",
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
			});

			var errors = new StringBuilder();
			var output = new StringBuilder();

			process.EnableRaisingEvents = true;

			process.OutputDataReceived += (s, d) => output.Append(d.Data);
			process.ErrorDataReceived += (s, d) => errors.Append(d.Data);

			var pid = process.Start();
			
			try
			{
				process.BeginErrorReadLine();
				process.BeginOutputReadLine();
			}
			catch (Exception ex)
			{
				throw ex;
			}

			process.WaitForExit();
			string stdout = output.ToString();
			string stderr = errors.ToString();

			var outFileInfo = new FileInfo(outfile + ".txt");
			//outfile += ".txt"; //Tesseract adds this automatically
			string result = null;
			if (outFileInfo.Exists)
			{
				var maxWait = DateTime.Now.AddSeconds(2);
				while (DateTime.Now < maxWait && outFileInfo.IsLocked())
				{
					Task.Delay(100).Wait();
				}
				if (outFileInfo.IsLocked())
					throw new FileLoadException($"File is still locked: {outFileInfo.FullName}");
				result = File.ReadAllText(outFileInfo.FullName);
				outFileInfo.Delete();
			} 
			else //if (process.ExitCode != 0)
			{
				//Tesseract Open Source OCR Engine v5.0.0.20190526 with Leptonica
				throw new Exception($"{process.ExitCode} {stderr}");
			}

			return result;
		}
	}
}
