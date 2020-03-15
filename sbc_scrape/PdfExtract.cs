using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace sbc_scrape
{
	public class PdfExtract
	{
		public static List<string> Extract(string filename)
		{
			var document = PdfReader.Open(filename);
			var pages = new List<PdfPage>();
			foreach (var page in document.Pages)
				pages.Add(page);

			var images = pages.SelectMany(page => ExtractImages(page).ToList()).ToList();

			//var fs = new FileStream($"{filenameNoExtension}.jpeg", FileMode.Create, FileAccess.Write);
			//var bw = new BinaryWriter(fs);
			//bw.Write(stream);
			//bw.Close();

			var tmp = pages.SelectMany(page => ExtractText(ContentReader.ReadContent(page)).ToList()).ToList();
			return tmp;
		}

		public static IEnumerable<ImageData> ExtractImages(PdfPage page)
		{
			return page.Elements.GetDictionary("/Resources")
				?.Elements.GetDictionary("/XObject")
				?.Elements.Values
				?.OfType<PdfReference>()
				.Select(o => o.Value)?.OfType<PdfDictionary>()
				.Where(o => o.Elements.GetString("/Subtype") == "/Image")
				.ToList().Select(o => ExtractImage(o));
		}

		public struct ImageData
		{
			public ImageData(string format, byte[] data)
			{
				Format = format;
				Data = data;
			}
			public string Format { get; }
			public byte[] Data { get; }
		}
		static ImageData ExtractImage(PdfDictionary image)
		{
			var filterArray = image.Elements.GetArray(PdfImage.Keys.Filter); //"/Filter");
			var filterList = filterArray.Select(o => o.ToString()).ToList();
			var filter = filterList[1]; // image.Elements.GetName("/Filter");
			switch (filter)
			{
				case "/DCTDecode":
					return new ImageData("jpg", ExtractJpegImage(image));
				case "/FlateDecode":
					return new ImageData("png", ExtractAsPngImage(image));
				default:
					return new ImageData("", new byte[] { });
			}
		}
		static byte[] ExtractJpegImage(PdfDictionary image)
		{
			// Fortunately JPEG has native support in PDF and exporting an image is just writing the stream to a file.
			return image.Stream.Value;
		}
		static byte[] ExtractAsPngImage(PdfDictionary image)
		{
			var width = image.Elements.GetInteger(PdfImage.Keys.Width);
			var height = image.Elements.GetInteger(PdfImage.Keys.Height);
			var bitsPerComponent = image.Elements.GetInteger(PdfImage.Keys.BitsPerComponent);

			var canUnfilter = image.Stream.TryUnfilter();
			var decoded = image.Stream.Value;

			//https://stackoverflow.com/questions/51049427/pdfsharp-extract-flatedecode-as-png
			// PdfSharp.Pdf.Advanced/PdfImage.cs to see how we create the PDF image formats.
			throw new NotImplementedException();
		}

		public static IEnumerable<string> ExtractText(CObject cObject)
		{
			if (cObject is COperator)
			{
				var cOperator = cObject as COperator;
				if (cOperator.OpCode.Name == OpCodeName.Tj.ToString() ||
					cOperator.OpCode.Name == OpCodeName.TJ.ToString())
				{
					foreach (var cOperand in cOperator.Operands)
						foreach (var txt in ExtractText(cOperand))
							yield return txt;
				}
			}
			else if (cObject is CSequence)
			{
				var cSequence = cObject as CSequence;
				foreach (var element in cSequence)
					foreach (var txt in ExtractText(element))
						yield return txt;
			}
			else if (cObject is CString)
			{
				var cString = cObject as CString;
				yield return cString.Value;
			}
		}
	}
}
