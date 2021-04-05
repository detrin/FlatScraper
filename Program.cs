using System;
using System.IO;
using System.Reflection;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using FlatScraper.Scrapers;
using FlatScraper.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace FlatScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json").Build();

            SrealityScraper srealityScraper = new SrealityScraper(config);
            srealityScraper.RunFlatsRent();          

        }
    }
}
