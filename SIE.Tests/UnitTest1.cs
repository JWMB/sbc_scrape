using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace SIE.Tests
{
	public class UnitTest1
	{
		[Fact]
		public async Task Test1()
		{
			var path = @"C:\Users\jonas\Downloads\output_20190929.se";
			var root = await SIEType.Read(path);
			var str = root.ToHierarchicalString();
		}

		[Fact]
		public void Test2()
		{
			var items = SIEType.ParseLine("1 2 \"string num 1\" asdd \"string num 2 !\" item");
			Assert.Equal(6, items.Length);
		}

	}
}
