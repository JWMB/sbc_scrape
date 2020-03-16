using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.Filters;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OCR
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
			var imageSections = page.Elements.GetDictionary("/Resources")
				?.Elements.GetDictionary("/XObject")
				?.Elements.Values
				?.OfType<PdfReference>()
				.Select(o => o.Value)?.OfType<PdfDictionary>()
				.Where(o => o.Elements?.GetString("/Subtype") == "/Image")
				?.ToList();

			return imageSections.Select(o =>
			{
				try
				{
					return ExtractImage(o);
				}
				catch
				{
					return new ImageData();
				}
			}) ?? new ImageData[] { };
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
		static ImageData ExtractImage(PdfDictionary pdfImage)
		{
			var couldUnfilter = pdfImage.Stream.TryUnfilter();
			if (!couldUnfilter)
			{
				List<string> filterList;
				try
				{
					var filterArray = pdfImage.Elements.GetArray(PdfImage.Keys.Filter);
					filterList = filterArray.Select(o => o.ToString()).ToList();
				}
				catch
				{
					filterList = new List<string> { pdfImage.Elements.GetString("/Filter") };
				}
				throw new NotSupportedException($"Could not unfilter {string.Join(",", filterList)}");
			}

			//var width = pdfImage.Elements.GetInteger(PdfImage.Keys.Width);
			//var height = pdfImage.Elements.GetInteger(PdfImage.Keys.Height);
			//var bitsPerComponent = pdfImage.Elements.GetInteger(PdfImage.Keys.BitsPerComponent);

			//// https://github.com/SixLabors/ImageSharp/issues/565
			//// https://github.com/SixLabors/ImageSharp/issues/606
			//Image? imageResult = null;
			//switch (bitsPerComponent)
			//{
			//	//case 1:
			//	//	using (image = new Image<SixLabors.ImageSharp.PixelFormats.>(width, height))
			//	//	{
			//	//		unsafe
			//	//		{
			//	//			fixed (Rgba32* pixelPtr = &MemoryMarshal.GetReference(image.GetPixelSpan()))
			//	//			{
			//	//				// interop code on pixelPtr
			//	//			}
			//	//		}
			//	//	}
			//		//break;
			//	case 8:
			//		using (var image = new Image<Gray8>(width, height))
			//		{
			//			unsafe
			//			{
			//				fixed (Gray8* pixelPtr = &MemoryMarshal.GetReference(image.GetPixelSpan()))
			//				{
			//				}
			//			}
			//			imageResult = image;
			//		}
			//		break;
			//	case 24:
			//		using (var image = new Image<Bgr24>(width, height))
			//		{
			//			unsafe
			//			{
			//				fixed (Bgr24* pixelPtr = &MemoryMarshal.GetReference(image.GetPixelSpan()))
			//				{
			//					// interop code on pixelPtr
			//				}
			//			}
			//		}
			//		break;
			//	default:
			//		throw new Exception("Unknown pixel format " + bitsPerComponent);
			//}
			//if (image != null)
			//	image.Save(output, jpegEncoder);


			//SixLabors.ImageSharp.ColorSpaces.RgbWorkingSpaces.

			//var bmp = new Bitmap(width, height, pixelFormat);
			//var bmd = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);
			//int length = (int)Math.Ceiling(Convert.ToInt32(width) * bitsPerComponent / 8.0);
			//for (int j = 0; j < height; j++)
			//{
			//	int offset = j * length;
			//	int scanOffset = j * bmd.Stride;
			//	Marshal.Copy(decoded, offset, new IntPtr(bmd.Scan0.ToInt32() + scanOffset), length);
			//}
			//bmp.UnlockBits(bmd);
			return new ImageData("", pdfImage.Stream.Value);
			//var filter = filterList[1]; // image.Elements.GetName("/Filter");
			//foreach (var filter in filterList)
			//{
			//	switch (filter)
			//	{
			//		case "/DCTDecode":
			//			return new ImageData("jpg", ExtractJpegImage(image));
			//		case "/FlateDecode":
			//			//new FlateDecode().Decode(new byte[] { });
			//			return new ImageData("png", ExtractAsPngImage(image));
			//		case "/ASCII85Decode":
			//			image.Stream.TryUnfilter();
			//			new Ascii85Decode().Decode()
			//		default:
			//			throw new NotSupportedException($"Filter {filter} not supported");
			//	}
			//}
			return new ImageData("", new byte[] { });
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
