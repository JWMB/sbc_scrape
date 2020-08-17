using OpenQA.Selenium;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using Scrape.IO.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
			if (string.IsNullOrEmpty(loginUrl)) throw new ArgumentNullException(nameof(loginUrl));
			if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
			if (string.IsNullOrEmpty(brfId)) throw new ArgumentNullException(nameof(brfId));

			var wait10 = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

			driver.Navigate().GoToUrl(loginUrl);

			driver.WaitUntilDocumentReady();

			var pid = driver.FindElement(By.Id("BankidLoginViewModel_PersonNumber")); //Changed dec 2019 from login_UserName
			pid.Clear();
			pid.SendKeys(username);

			var btn = driver.FindElement(By.XPath("//button[@type='submit' and contains(., 'BankID')]")); // Changed spring 2020 from By.CssSelector("form > button")); //Changed dec 2019 from By.Id("login_Login_Button"));
			btn.Click();

			var finder = By.XPath($"//a[text()='{brfId}']"); // Changed dec 2019 from "//input[@type='submit' and @value='{brfId}']");
			new WebDriverWait(driver, TimeSpan.FromMinutes(1)).Until(WebDriverExtensions.ElementIsPresent(finder));
			var element = driver.FindElement(finder);
			if (element == null)
				throw new NotFoundException($"Text {brfId} not found");
			await Task.Delay(500);
			try
			{
				element.Click();
			}
			catch (Exception ex)
			{
				throw;
			}

			await Task.Delay(500);
			driver.WaitUntilDocumentReady();
		}

		public void LoginToMediusFlow(string url)
		{
			Go();
			var ensureHost = new Uri(url).Host;
			while (!driver.Url.Contains(ensureHost))
			{
				Thread.Sleep(500);
				Go();
			}

			void Go()
			{
				// https://stackoverflow.com/questions/17547473/how-to-open-a-new-tab-using-selenium-webdriver
				driver.Navigate().GoToUrl(url);
				driver.WaitUntilDocumentReady();
			}
		}

		public string GetMediusFlowCSRFToken()
		{
			//var csrfToken = driver.FindElementsByXPath("//input[@name='__RequestVerificationToken']").FirstOrDefault()?.GetAttribute("value");
			var js = driver.FindElementsByXPath("//script[contains(., 'antiForgeryToken.init')]").FirstOrDefault()?.GetAttribute("innerText") ?? "";
			var m = Regex.Match(js, @"(?<=antiForgeryToken\.init.+value=\"")[^\""]+");
			return m.Value;
		}

		public Task<string> FetchHtmlSource(string urlPath, int year, int monthFrom = 1, int monthTo = 12)
		{
			driver.NavigateAndWaitReadyIfNotThere("https://varbrf.sbc.se/" + urlPath);
			//var accountSelect = driver.FindElement(By.XPath("//select[contains(@id,'_DDKonto')]"));

			List<IWebElement> FindXPathAndPredicate(string xpath, Func<IWebElement, bool> predicate)
			{
				var elOptions = driver.FindElements(By.XPath(xpath));
				var matches = new List<IWebElement>();
				foreach (var el in elOptions)
					if (predicate(el))
						matches.Add(el);
				return matches;
			}

			void ClickSelectOption(string selectIdContains, string optionText)
			{
				var xpath = $"//select[contains(@id,'{selectIdContains}')]/option[text() = '{optionText}']";
				try
				{
					var elOption = driver.FindElement(By.XPath(xpath));
					if (elOption != null)
						elOption.Click();
				}
				catch (Exception ex)
				{
					throw new Exception($"Unable to locate Select/Option {optionText} ({selectIdContains} for {urlPath})", ex);
				}
			}

			ClickSelectOption("PeriodFrom", monthFrom.ToString());
			ClickSelectOption("PeriodTom", monthTo.ToString());
			//ClickSelectOption("Ar", year.ToString());
			var found = FindXPathAndPredicate($"//select[contains(@id,'Ar')]/option", el => el.Text.StartsWith(year.ToString())).FirstOrDefault();
			found?.Click();

			driver.FindElement(By.XPath("//input[@type='submit']")).Click();
			driver.WaitUntilDocumentReady();

			var fullpage = driver.PageSource;
			return Task.FromResult(fullpage);
		}
	}
}
