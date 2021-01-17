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
		private readonly Fetcher fetcher;

		public SBCMain(RemoteWebDriver driver, Fetcher fetcher)
		{
			this.driver = driver;
			this.fetcher = fetcher;
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

			// Select BankID login
			var btn = driver.FindElement(By.XPath("//button[@type='submit' and contains(., 'BankID')]")); // Changed spring 2020 from By.CssSelector("form > button")); //Changed dec 2019 from By.Id("login_Login_Button"));
			btn.Click();

			// Waiting for user to login
			// TODO: not sure what the doc looks like when choosing between roles...
			new WebDriverWait(driver, TimeSpan.FromMinutes(1)).Until(d => d.FindElements(By.Id("loginModal")).Any() == false);

			// We might have to select a role here - otherwise we'll be at portal main page
			if (!driver.Url.EndsWith("/Portalen"))  //https://varbrf.sbc.se
			{
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

		public async Task<string> FetchSIEFile(int year)
		{
			//Load what's in the relevant iFrame:
			driver.NavigateAndWaitReadyIfNotThere("https://varbrf.sbc.se/eos/hg2/reports/19/?embed=1");
			var scriptWithDef = driver.FindElement(By.XPath("//script[@id='report-layout-str']"));
			var json = Newtonsoft.Json.Linq.JObject.Parse(scriptWithDef.GetAttribute("innerHTML"));
			var nodes = json.SelectTokens("$..widgets..data.nodes[*]");
			var opop = nodes.Where(n => n.Value<string>("title").StartsWith(year.ToString())).FirstOrDefault();
			var idForSelectedYear = opop.Value<string>("id");

			return await GetSIEForId(idForSelectedYear);

			//var url = "https://varbrf.sbc.se/Portalen/Ekonomi/Revisor/SIE/";
			//driver.NavigateAndWaitReadyIfNotThere(url);

			//"regions": {
			//			"top": {
			//				"rows": [
			//			{
			//				 "widgets": [
			//                  "data": {
			//				"nodes": [
			//								{
			//					"disabled": false,
			//                                       "level": 0,
			//                                       "selected": false,
			//                                       "id": "__nochoice__",
			//                                       "title": "Inget val"
			//								},


			//var yearDropdown = "//div[contains(@class, 'tiki-dropdown')]/button";
			//var buttonSelectYear = driver.FindElement(By.XPath(yearDropdown));
			//buttonSelectYear.Click();

			//var selectedYearElement = driver.FindElement(By.XPath($"//div[contains(@class, 'tiki-dropdown-optionsview')]//li[contains(text(), '{year}')]"));
			//var idForSelectedYear = selectedYearElement.GetAttribute("id");
			//selectedYearElement.Click();

			//var buttonCreate = driver.FindElement(By.XPath("//button[contains(text(), 'Skapa SIE-fil')]"));
			//buttonCreate.Click();

			async Task<string> GetSIEForId(string id)
			{
				var setYearUrl = "https://varbrf.sbc.se/eos/hg2/protocol/list/select/workspaces[19].axes[2]";
				var config = new Scrape.IO.FetchConfig
				{
					Method = Scrape.IO.MethodMode.Post,
					Headers = new Dictionary<string, string> {
						{ "Content-Type", "application/x-www-form-urlencoded; charset=UTF-8" }
					},
					Body = new Dictionary<string, string> { { "data", $"[\"{id}\"]" } } //$"[\"{id}\"]"
				};
				var resultSetYear = await fetcher.Fetch(setYearUrl, config);

				var getSIEUrl = $"https://varbrf.sbc.se/eos/appspecific/Webbeko/extraobjects/SIEExport.php?ws=19&object=extraobjects[1]";
				var result = await fetcher.Fetch(getSIEUrl, new Scrape.IO.FetchConfig { Method = Scrape.IO.MethodMode.Get });
				return result.Body?.ToString();
			}
		}

		public Task<string> FetchHtmlSource(string urlPath, int year, int monthFrom = 1, int monthTo = 12)
		{
			var url = "https://varbrf.sbc.se/" + urlPath;
			driver.NavigateAndWaitReadyIfNotThere(url);
			if (!driver.Url.ToLower().StartsWith(url.ToLower()))
				throw new ArgumentException($"Was redirected from '{url}' to '{driver.Url}'");

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
				var tryXPaths = new[] { $"text() = '{optionText}'", $"@value = '{optionText}'" }
					.Select(o => $"//select[contains(@id,'{selectIdContains}')]/option[{o}]");
				var exceptions = new List<Exception>();
				foreach (var xpath in tryXPaths)
				{
					try
					{
						var elOption = driver.FindElement(By.XPath(xpath));
						if (elOption != null)
						{
							elOption.Click();
							return;
						}
					}
					catch (Exception ex)
					{
						exceptions.Add(ex);
					}
				}
				throw new AggregateException($"Unable to locate Select/Option {optionText} ({selectIdContains} for {urlPath})", exceptions);
			}

			ClickSelectOption("PeriodFrom", monthFrom.ToString());
			try
			{
				ClickSelectOption("PeriodTom", monthTo.ToString());
			}
			catch (Exception ex)
			{
				ClickSelectOption("PeriodTo", monthTo.ToString()); // Changed to PeriodTo for some pages aug 2020
			}

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
