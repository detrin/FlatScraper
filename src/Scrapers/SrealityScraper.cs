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
using Serilog;

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
        private readonly int WorkersCount;
        private Serilog.Core.Logger log;

        public SrealityScraper(IConfigurationRoot configurationRoot)
        {
            this.ConfigurationRoot = configurationRoot;
            var configDatabase = configurationRoot.GetSection("Database").Get<Dictionary<string, string>>();
            string useSecret = configDatabase["useSecret"];
            DatabaseSecret = configDatabase[useSecret];

            WorkersCount = configurationRoot.GetSection("workersCount").Get<int>();

            xpaths.Add("isLoaded", "/html/body/div[2]/div[1]/div[2]/div[2]/div[5]/preact/div/div/p[2]");
            xpaths.Add("numItemsButton", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/span[2]/span[2]/span/span[1]");
            xpaths.Add("itemsOf60", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/span[2]/span[2]/span/span[2]/ul/li[2]/button");
            xpaths.Add("lastItem", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[3]/div/div[61]/a");
            xpaths.Add("endPageItem", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[2]/div/div[1]/img");
            // Extracting info from site
            xpaths.Add("titleProp", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[4]/h1/span/span[1]");
            xpaths.Add("address", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[4]/h1/span/span[2]/span");
            xpaths.Add("priceShort", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[4]/span/span[2]");
            xpaths.Add("offerProps", "/html/body/div[2]/div[1]/div[2]/div[2]/div[4]/div/div/div/div/div[6]");

            log = new LoggerConfiguration()
                .WriteTo.File("logExceptions.txt")
                .CreateLogger();
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
                string url = $"https://www.sreality.cz/hledani/prodej/byty/praha?strana={pageNum}";
                chromeDriver.Navigate().GoToUrl(url);
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
                string url = "";
                while (itemsPresent)
                {
                    url = $"{BaseUrl}?strana={lastPageNum}";
                    lastPageNum++;
                    chromeDriver.Navigate().GoToUrl(url);
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
                if (flatOfferSaved.Link == flatOffer.Link && !flatOffer.Equals(flatOfferSaved))
                {
                    Console.WriteLine($"updated {flatOfferSaved.Link}");
                    flatOfferSaved.AddState(flatOffer.State);
                    db.UpsertRecord<FlatOffer>(CollectionName, flatOfferSaved.Link, flatOfferSaved);
                }
            }
        }

        public Task UpdateOfferInfo(ChromeDriver chromeDriver)
        {
            return Task.Run(() =>
            {
                MongoCRUD db = new MongoCRUD(DatabaseSecret, "FlatScraper");
                while (offerNum < offerLinks.Count)
                {
                    string url = "";
                    try
                    {
                        url = offerLinks[offerNum];
                        offerNum++;
                        Console.WriteLine($"{offerNum}/{offerLinks.Count}");
                        chromeDriver.Navigate().GoToUrl(url);
                        WaitTillLoaded(chromeDriver, "offerProps", 30);

                        Dictionary<string, string> stateProps = new Dictionary<string, string>();
                        // title properties
                        string titleProp = chromeDriver.FindElement(By.XPath(xpaths["titleProp"])).Text;
                        stateProps["titleProp"] = titleProp;
                        string address = chromeDriver.FindElement(By.XPath(xpaths["address"])).Text;
                        stateProps["address"] = address;
                        // string priceShort = chromeDriver.FindElement(By.XPath(xpaths["priceShort"])).Text;
                        // stateProps["priceShort"] = priceShort;

                        // body properties
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
                            }
                            else
                            {
                                IWebElement propIcon = prop.FindElements(By.TagName("span"))[1];
                                string iconClass = propIcon.GetAttribute("class");
                                stateProps[propName] = iconClass;
                            }

                        }

                        FlatOffer newFlatOffer = new FlatOffer
                        {
                            Link = url,
                            State = new FlatOfferState
                            {
                                LastChecked = DateTime.UtcNow,
                                Created = DateTime.UtcNow,
                                Properties = stateProps
                            },
                        };

                        UpdateSavedOffer(db, newFlatOffer);

                    }
                    catch (Exception e)
                    {
                        log.Information(url);
                        log.Information(e.ToString());
                    }

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

        public void CheckForDelistedOffers()
        {
            MongoCRUD db = new MongoCRUD(DatabaseSecret, "FlatScraper");
            List<FlatOffer> flatOffers = db.LoadRecords<FlatOffer>(CollectionName);
            foreach (FlatOffer flatOffer in flatOffers)
            {
                if (flatOffer.State.Delisted == null && !offerLinks.Contains(flatOffer.Link))
                {
                    // Console.WriteLine($"{offerLinks.Contains(flatOffer.Link)}");
                    var timeStamp = DateTime.UtcNow;
                    flatOffer.State.Delisted = timeStamp;
                    flatOffer.State.LastChecked = timeStamp;
                    db.UpsertRecord<FlatOffer>(CollectionName, flatOffer.Link, flatOffer);
                }
            }
        }

        public void RunFlatsBuy()
        {
            // Fetching offers
            BaseUrl = "https://www.sreality.cz/hledani/prodej/byty/praha";
            CollectionName = "SrealityFlatBuy";

            // Fetching data
            Visibility = false;
            ChromeOptions chromeOptions = GetOptions();
            int driverCount = 5;
            Console.WriteLine("fetching offer links ...");
            FetchOfferLinks(chromeOptions, driverCount);
            Console.WriteLine($"Total links: {offerLinks.Count}");
            offerLinks = offerLinks.Distinct().ToList();
            Console.WriteLine($"Total reduced links: {offerLinks.Count}");

            // Check for delisted offers
            CheckForDelistedOffers();

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

            // Fetching data
            Visibility = false;
            ChromeOptions chromeOptions = GetOptions();
            Console.WriteLine("fetching offer links ...");
            FetchOfferLinks(chromeOptions, WorkersCount);
            Console.WriteLine($"Total links: {offerLinks.Count}");
            offerLinks = offerLinks.Distinct().ToList();
            Console.WriteLine($"Total reduced links: {offerLinks.Count}");

            // Check for delisted offers
            CheckForDelistedOffers();

            // Updating data in database
            Visibility = false;
            chromeOptions = GetOptions();
            Console.WriteLine("updating offer links ...");
            SaveOfferLinks(chromeOptions, WorkersCount);

            Console.WriteLine($"{DatabaseSecret}");
        }
    }
}