using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using Scrape.IO.Selenium;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SBCScan.SBC
{
	public class SBCMain
	{
		private readonly RemoteWebDriver driver;

		public SBCMain(RemoteWebDriver driver)
		{
			this.driver = driver;
		}

		public async Task Login(string loginUrl, string username, string brfId)
		{
			var wait10 = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

			driver.Navigate().GoToUrl(loginUrl);

			driver.WaitUntilDocumentReady();

			var pid = driver.FindElement(By.Id("login_UserName"));
			pid.Clear();
			pid.SendKeys(username);

			var btn = driver.FindElement(By.Id("login_Login_Button"));
			btn.Click();

			var finder = By.XPath($"//input[@type='submit' and @value='{brfId}']");
			//var finder = By.Id("Forening_picker_login_Login_select_forening_1");
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

		public async Task<string> FetchHtmlSource(string urlPath, int year, int monthFrom = 1, int monthTo = 12)
		{
			driver.NavigateAndWaitReadyIfNotThere("https://varbrf.sbc.se/" + urlPath);
			//var accountSelect = driver.FindElement(By.XPath("//select[contains(@id,'_DDKonto')]"));

			void ClickSelectOption(string selectIdContains, string optionText)
			{
				var xpath = $"//select[contains(@id,'{selectIdContains}')]/option[text() = '{optionText}']";
				var elOption = driver.FindElement(By.XPath(xpath));
				if (elOption != null)
					elOption.Click();
			}

			ClickSelectOption("PeriodFrom", monthFrom.ToString());
			ClickSelectOption("PeriodTom", monthTo.ToString());
			ClickSelectOption("Ar", year.ToString());

			driver.FindElement(By.XPath("//input[@type='submit']")).Click();
			driver.WaitUntilDocumentReady();

			var fullpage = driver.PageSource;
			return fullpage;
		}
	}
}
