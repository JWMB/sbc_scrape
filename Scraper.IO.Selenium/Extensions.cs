using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Scrape.IO.Selenium
{
	public static class WebDriverExtensions
	{
		public static Func<IWebDriver, bool> ElementIsPresent(By by)
		{
			return driver =>
			{
				try { driver.FindElement(by); return true; }
				catch { return false; }
			};
		}

		public static Func<IWebDriver, bool> ElementIsVisible(IWebElement element)
		{
			return driver =>
			{
				try { return element.Displayed; }
				catch { return false; }
			};
		}
		public static void WaitUntilDocumentReady(this IWebDriver driver, int timeoutSeconds = 60)
		{
			new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds)).Until(
	d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
		}
	}
}
