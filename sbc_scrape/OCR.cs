using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace sbc_scrape
{
	class OCR
	{
		public static string Run(string imageFile, IEnumerable<string> languages)
		{
			var pathToTesseract = @"C:\Users\jonas\AppData\Local\Tesseract-OCR";
			var outfile = Path.Combine(Directory.GetCurrentDirectory(), $"ocr_tmp{DateTime.Now.Ticks % 100000}");
			var process = Process.Start(new ProcessStartInfo {
				FileName = Path.Combine(pathToTesseract, "tesseract.exe"),
				Arguments = $"\"{imageFile}\" \"{outfile}\" -l {(string.Join("+", languages))}",
				//CreateNoWindow = true,
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
			{ }

			process.WaitForExit();
			string stdout = output.ToString();
			string stderr = errors.ToString();

			outfile += ".txt"; //Tesseract adds this automatically
			string result = null;
			if (File.Exists(outfile))
			{
				result = File.ReadAllText(outfile);
				File.Delete(outfile);
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
