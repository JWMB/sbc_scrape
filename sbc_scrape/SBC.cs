using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using Scrape.IO.Selenium;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan
{
	public class SBC
	{
		private readonly RemoteWebDriver driver;

		public SBC(RemoteWebDriver driver)
		{
			this.driver = driver;
		}

		public async Task Login(string loginUrl, string username)
		{
			var wait10 = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

			driver.Navigate().GoToUrl(loginUrl);

			driver.WaitUntilDocumentReady();

			var pid = driver.FindElement(By.Id("login_UserName"));
			pid.Clear();
			pid.SendKeys(username);

			var btn = driver.FindElement(By.Id("login_Login_Button"));
			btn.Click();

			var finder = By.Id("Forening_picker_login_Login_select_forening_1");
			new WebDriverWait(driver, TimeSpan.FromMinutes(4)).Until(WebDriverExtensions.ElementIsPresent(finder));
			driver.FindElement(finder).Click();

			await Task.Delay(500);
			//System.Threading.Thread.Sleep(1000);
			driver.WaitUntilDocumentReady();
		}

		public void LoginToMediusFlow(string url)
		{
			// https://stackoverflow.com/questions/17547473/how-to-open-a-new-tab-using-selenium-webdriver
			driver.Navigate().GoToUrl(url);
			driver.WaitUntilDocumentReady();
		}
	}
}
