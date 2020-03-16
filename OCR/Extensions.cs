using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OCR
{
	public static class FileInfoExtensions
	{
		public static bool IsLocked(this FileInfo file)
		{
			FileStream? stream = null;
			try
			{
				stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
			}
			catch (IOException)
			{
				return true;
			}
			finally
			{
				if (stream != null)
					stream.Close();
			}

			return false;
		}
	}
}
