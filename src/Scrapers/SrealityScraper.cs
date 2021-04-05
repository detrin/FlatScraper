using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using FlatScraper.Scrapers;
using ShellProgressBar;
using System.Threading.Tasks;
using System.Threading;
using FlatScraper.Models;
using Microsoft.Extensions.Configuration;

namespace FlatScraper.Scrapers
{
    public class SrealityScraper : Scraper
    {
        protected int lastPageNum = 1;
        protected int offerNum = 0;
        protected List<string> offerLinks = new List<string>();
        protected Dictionary<string, bool> propsDict = new Dictionary<string, bool>();
        private readonly IConfigurationRoot ConfigurationRoot;
        private readonly string DatabaseSecret;

        public SrealityScraper(IConfigurationRoot configurationRoot)
        {
            this.ConfigurationRoot = configurationRoot;
            var configDatabase = configurationRoot.GetSection("Database").Get<Dictionary<string, string>>();
            string useSecret = configDatabase["useSecret"];
            DatabaseSecret = configDatabase[useSecret];
        }


        protected bool _IsElementPresent(ChromeDriver chromeDriver, string xpath)
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

        public void SelectNumberOfItems(ChromeDriver chromeDriver)
        {
            ClickElement(chromeDriver, "numItemsButton");
            ClickElement(chromeDriver, "itemsOf60");
        }

        public IEnumerable<string> GetOfferLinks(ChromeDriver chromeDriver)
        {
            for (int item = 1; item <= 61; item++)
            {
                string xpath = $"/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[3]/div/div[{item}]/a";
                string link = "";
                try
                {
                    IWebElement element = chromeDriver.FindElement(By.XPath(xpaths["numItemsButton"]));
                    link = element.GetAttribute("href");
                }
                catch (NoSuchElementException)
                {
                }
                if (link != "")
                    yield return link;

            }
        }

        public IEnumerator<bool> IterateOverSites(ChromeDriver chromeDriver)
        {
            bool itemsPresent = true;
            int pageNum = 1;
            while (itemsPresent)
            {
                Url = $"https://www.sreality.cz/hledani/prodej/byty/praha?strana={pageNum}";
                chromeDriver.Navigate().GoToUrl(Url);
                var wait = new WebDriverWait(chromeDriver, new TimeSpan(0, 0, 30));
                wait.Until(IsAnyOfElementsPresent(By.XPath(xpaths["numItemsButton"]), By.XPath(xpaths["endPageItem"])));
                try
                {
                    chromeDriver.FindElement(By.XPath(xpaths["numItemsButton"]));
                    itemsPresent = true;
                }
                catch (NoSuchElementException)
                {
                    itemsPresent = false;
                }
                if (itemsPresent && chromeDriver.FindElement(By.XPath(xpaths["numItemsButton"])).Text != "60")
                {
                    SelectNumberOfItems(chromeDriver);
                    WaitTillLoaded(chromeDriver, "lastItem", 3);
                }
                yield return itemsPresent;
                pageNum++;
            }
        }

        public Task GetLinksOnSite(ChromeDriver chromeDriver)
        {
            return Task.Run(() =>
            {
                bool itemsPresent = true;
                while (itemsPresent)
                {
                    Url = $"{BaseUrl}?strana={lastPageNum}";
                    lastPageNum++;
                    chromeDriver.Navigate().GoToUrl(Url);
                    var wait = new WebDriverWait(chromeDriver, new TimeSpan(0, 0, 30));
                    wait.Until(IsAnyOfElementsPresent(By.XPath(xpaths["numItemsButton"]), By.XPath(xpaths["endPageItem"])));
                    //var itemsPresent = _isElementPresent(chromeDriver, xpaths["numItemsButton"]);
                    try
                    {
                        chromeDriver.FindElement(By.XPath(xpaths["numItemsButton"]));
                        itemsPresent = true;
                    }
                    catch (NoSuchElementException)
                    {
                        itemsPresent = false;
                    }
                    if (itemsPresent)
                    {
                        if (chromeDriver.FindElement(By.XPath(xpaths["numItemsButton"])).Text != "60")
                        {
                            SelectNumberOfItems(chromeDriver);
                            WaitTillLoaded(chromeDriver, "lastItem", 3);
                        }

                        for (int item = 1; item <= 61; item++)
                        {

                            try
                            {
                                string xpath = $"/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[3]/div/div[{item}]/a";
                                IWebElement element = chromeDriver.FindElement(By.XPath(xpath));
                                string offerLink = element.GetAttribute("href");
                                offerLinks.Add(offerLink);
                            }
                            catch (NoSuchElementException)
                            {
                            }
                        }
                    }
                }
            });
        }

        public void FetchOfferLinks(ChromeOptions chromeOptions, int driverCount)
        {
            var chromeDrivers = CreateBrowsers(driverCount, chromeOptions);
            try
            {
                List<Task> driverTasks = new List<Task>();
                for (int browserNum = 0; browserNum < driverCount; browserNum++)
                {
                    driverTasks.Add(GetLinksOnSite(chromeDrivers[browserNum]));
                    Thread.Sleep(2000);
                }
                Task.WaitAll(driverTasks.ToArray());
            }
            finally
            {
                foreach (ChromeDriver chromeDriver in chromeDrivers)
                {
                    chromeDriver.Dispose();
                }
            }
        }

        public void UpdateSavedOffer(MongoCRUD db, FlatOffer flatOffer)
        {
            FlatOffer flatOfferSaved = db.LoadRecordsById<FlatOffer>(CollectionName, flatOffer.Link);
            if (flatOfferSaved == null)
            {
                db.InsertRecord<FlatOffer>(CollectionName, flatOffer);
            }
            else
            {
                Console.WriteLine($"{flatOfferSaved.Link} {flatOffer.Link}");
                
                if (flatOfferSaved.Link == flatOffer.Link && !flatOffer.Equals(flatOfferSaved))
                {
                    flatOfferSaved.AddState(flatOffer.State);          
                    db.UpsertRecord<FlatOffer>(CollectionName, flatOfferSaved.Link, flatOfferSaved);
                }
            }
        }

        public Task UpdateOfferInfo(ChromeDriver chromeDriver)
        {
            return Task.Run(() =>
            {
                Console.WriteLine($"{DatabaseSecret}");
                
                MongoCRUD db = new MongoCRUD(DatabaseSecret, "FlatScraper");
                while (offerNum < offerLinks.Count)
                {
                    Url = offerLinks[offerNum];
                    offerNum++;
                    Console.WriteLine($"{offerNum}/{offerLinks.Count}");
                    chromeDriver.Navigate().GoToUrl(Url);
                    WaitTillLoaded(chromeDriver, "offerProps", 30);

                    Dictionary<string, string> stateProps = new Dictionary<string, string>();
                    IWebElement offerProps = chromeDriver.FindElement(By.XPath(xpaths["offerProps"]));
                    IReadOnlyList<IWebElement> propComposites = offerProps.FindElements(By.TagName("li"));
                    foreach (IWebElement prop in propComposites)
                    {
                        IWebElement propNameElement = prop.FindElement(By.TagName("label"));
                        string propName = propNameElement.Text.Split(':')[0];
                        IWebElement propValueElement = prop.FindElement(By.TagName("strong"));
                        string propValue = propValueElement.Text;
                        if (propValue != "")
                        {
                            stateProps[propName] = propValue;
                        } else {
                            IWebElement propIcon = prop.FindElements(By.TagName("span"))[1];
                            string iconClass = propIcon.GetAttribute("class");
                            stateProps[propName] = iconClass;
                        }
                        
                    }

                    FlatOffer newFlatOffer = new FlatOffer
                    {
                        Link = Url,
                        State = new FlatOfferState
                        {
                            LastChecked = DateTime.UtcNow,
                            Created = DateTime.UtcNow,
                            Properties = stateProps
                        },
                    };

                    UpdateSavedOffer(db, newFlatOffer);
                }
            });
        }

        public void SaveOfferLinks(ChromeOptions chromeOptions, int driverCount)
        {
            var chromeDrivers = CreateBrowsers(driverCount, chromeOptions);
            try
            {
                List<Task> driverTasks = new List<Task>();
                for (int browserNum = 0; browserNum < driverCount; browserNum++)
                {
                    driverTasks.Add(UpdateOfferInfo(chromeDrivers[browserNum]));
                    Thread.Sleep(2000);
                }
                Task.WaitAll(driverTasks.ToArray());
            }
            finally
            {
                foreach (ChromeDriver chromeDriver in chromeDrivers)
                {
                    chromeDriver.Dispose();
                }
            }
        }

        public void RunFlatsBuy()
        {
            // Fetching offers
            BaseUrl = "https://www.sreality.cz/hledani/prodej/byty/praha";
            CollectionName = "SrealityFlatBuy";
            xpaths.Add("isLoaded", "/html/body/div[2]/div[1]/div[2]/div[2]/div[5]/preact/div/div/p[2]");
            xpaths.Add("numItemsButton", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/span[2]/span[2]/span/span[1]");
            xpaths.Add("itemsOf60", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/span[2]/span[2]/span/span[2]/ul/li[2]/button");
            xpaths.Add("lastItem", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[3]/div/div[61]/a");
            xpaths.Add("endPageItem", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/div/div[1]/img");
            // Extracting info from site
            xpaths.Add("offerProps", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[6]");

            // Fetching data
            Visibility = false;
            ChromeOptions chromeOptions = GetOptions();
            int driverCount = 5;
            Console.WriteLine("fetching offer links ...");
            FetchOfferLinks(chromeOptions, driverCount);
            Console.WriteLine($"Total links: {offerLinks.Count}");
            offerLinks = offerLinks.Distinct().ToList();
            Console.WriteLine($"Total reduced links: {offerLinks.Count}");
            
            // Updating data in database
            Visibility = false;
            chromeOptions = GetOptions();
            driverCount = 2;
            Console.WriteLine("updating offer links ...");
            SaveOfferLinks(chromeOptions, driverCount);
            
            Console.WriteLine($"{DatabaseSecret}");
        }

        public void RunFlatsRent()
        {
            // Fetching offers
            BaseUrl = "https://www.sreality.cz/hledani/pronajem/byty/praha";
            CollectionName = "SrealityFlatRent";
            xpaths.Add("isLoaded", "/html/body/div[2]/div[1]/div[2]/div[2]/div[5]/preact/div/div/p[2]");
            xpaths.Add("numItemsButton", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/span[2]/span[2]/span/span[1]");
            xpaths.Add("itemsOf60", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/span[2]/span[2]/span/span[2]/ul/li[2]/button");
            xpaths.Add("lastItem", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[3]/div/div[61]/a");
            xpaths.Add("endPageItem", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/div/div[1]/img");
            // Extracting info from site
            xpaths.Add("offerProps", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[6]");

            // Fetching data
            Visibility = false;
            ChromeOptions chromeOptions = GetOptions();
            int driverCount = 5;
            Console.WriteLine("fetching offer links ...");
            FetchOfferLinks(chromeOptions, driverCount);
            Console.WriteLine($"Total links: {offerLinks.Count}");
            offerLinks = offerLinks.Distinct().ToList();
            Console.WriteLine($"Total reduced links: {offerLinks.Count}");
            
            // Updating data in database
            Visibility = false;
            chromeOptions = GetOptions();
            driverCount = 5;
            Console.WriteLine("updating offer links ...");
            SaveOfferLinks(chromeOptions, driverCount);
            
            Console.WriteLine($"{DatabaseSecret}");
        }
    }
}