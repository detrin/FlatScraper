using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace FlatScraper.Scrapers
{
    public class Scraper
    {
        protected string browserDriverPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        protected Dictionary<string, string> xpaths = new Dictionary<string, string>();
        protected float waitAfterAction = 0.2f;

        public string Url { get; set; }
        public string BaseUrl { get; set; }
        public bool Visibility { get; set; }
        public string UserAgent { get; set; }
        public string CollectionName { get; set; }

        protected ChromeOptions GetOptions()
        {
            ChromeOptions options = new ChromeOptions();
            // options.AddArguments("--start-maximized");
            options.AddArguments($"user-agent={UserAgent}");
            if (!Visibility)
            {
                options.AddArguments("--headless");
                options.AddArguments("--no-sandbox");
                options.AddArguments("--disable-dev-shm-usage");
            }
            return options;
        }

        protected IWebElement WaitTillLoaded(ChromeDriver chromeDriver, string xpathElementName)
        {
            var wait = new WebDriverWait(chromeDriver, new TimeSpan(0, 0, 1, 0));
            IWebElement element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath(xpaths[xpathElementName])));
            return element;
        }

        protected IWebElement WaitTillLoaded(ChromeDriver chromeDriver, string xpathElementName, int loadSeconds)
        {
            var wait = new WebDriverWait(chromeDriver, new TimeSpan(0, 0, 0, loadSeconds));
            IWebElement element = wait.Until(
              SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.XPath(xpaths[xpathElementName]))
            );
            return element;
        }

        protected void ClickElement(ChromeDriver chromeDriver, string xpathElementName)
        {
            IWebElement element = chromeDriver.FindElement(By.XPath(xpaths[xpathElementName]));
            element.Click();
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(waitAfterAction));
        }

        protected virtual bool IsElementPresent(ChromeDriver chromeDriver, By by)
        {
            try
            {
                chromeDriver.FindElement(by);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        protected virtual bool IsElementPresent(ChromeDriver chromeDriver, string xpath)
        {
            try
            {
                chromeDriver.FindElement(By.XPath(xpath));
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        protected Func<IWebDriver, bool> IsAnyOfElementsPresent(By by1, By by2)
        {
            return (driver) =>
                {
                    try
                    {
                        IWebElement e1 = driver.FindElement(by1);
                        return e1.Displayed;
                    }
                    catch (Exception)
                    {
                        try
                        {
                            IWebElement e2 = driver.FindElement(by2);
                            return e2.Displayed;
                        }
                        catch (Exception)
                        {
                            // If element is null, stale or if it cannot be located
                            return false;
                        }
                    }
                };
        }

        protected List<ChromeDriver> CreateBrowsers(int numberOfBrowsers, ChromeOptions options)
        {
            List<ChromeDriver> browsers = new List<ChromeDriver>();
            for (int i = 0; i < numberOfBrowsers; i++)
            {
                ChromeDriver chromeDriver = new ChromeDriver(browserDriverPath, options);
                browsers.Add(chromeDriver);
            }
            return browsers;
        }

    }
}
