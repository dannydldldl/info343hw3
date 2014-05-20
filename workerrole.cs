using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using System.Globalization;
using HtmlAgilityPack;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        public Boolean on;
        public HashSet<String> disallowedWordSet;
        public HashSet<String> visitedUrlSet;
        public DateTime currentDateTime;
        public CloudQueue linkQueue;
        public CloudQueue adminQueue;
        public CloudTable linkTable;
        public CloudTable performanceTable;
        public int tableSize;
        public int queueSize;
        public int totalUrlCrawled;

        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("WorkerRole1 entry point called");

            this.on = false;
            this.disallowedWordSet = new HashSet<String>();
            this.visitedUrlSet = new HashSet<String>();
            this.currentDateTime = DateTime.Now;
            QueueConnection();
            TableConnection();
            this.tableSize = 0;
            this.queueSize = 0;
            this.totalUrlCrawled = 0;
            
            while (true)
            {
                CheckAdminQueue();
                if(on)
                {
                    CrawlFromQueue();
                }
                Thread.Sleep(500);
                Trace.TraceInformation("Working");
            }
        }

        public Boolean CheckAdminQueue()
        {
            string command = GetMessageFromQueue(adminQueue);
            if (command != null)
            {
                if (command.Equals("start"))
                {
                    on = true;
                    InitializeQueue("http://www.cnn.com/robots.txt");
                    InitializeQueue("http://sportsillustrated.cnn.com/robots.txt");
                }
                else if (command.Equals("stop"))
                {
                    on = false;
                }
                else if (command.Equals("refresh"))
                {
                    UpdatePerformance();
                }
            }
            return on;
        }

        public void TableConnection()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            linkTable = tableClient.GetTableReference("linktable");
            linkTable.CreateIfNotExists();
            performanceTable = tableClient.GetTableReference("performancetable");
            performanceTable.CreateIfNotExists();
        }

        public void QueueConnection()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            linkQueue = queueClient.GetQueueReference("linkqueue");
            linkQueue.CreateIfNotExists();
            adminQueue = queueClient.GetQueueReference("adminqueue");
            adminQueue.CreateIfNotExists();
        }


        public string GetMessageFromQueue(CloudQueue queue)
        {
            CloudQueueMessage message = queue.GetMessage(TimeSpan.FromSeconds(1));
            if (message == null)
            {
                return null; //no message to read
            }
            queue.DeleteMessage(message);
            if(queue.ToString() == "linkQueue")
            {
                queueSize--;
            }
            return message.AsString;
        }


        //access the html tag, find title
        public void CrawlFromQueue()
        {
            string url = GetMessageFromQueue(linkQueue);
            if (url != null)//queue is not null, has message
            {
                HtmlWeb htmlWeb = new HtmlWeb();
                HtmlDocument document = htmlWeb.Load(url);
                HtmlNode lastmodHtmlNode = document.DocumentNode.SelectSingleNode("//meta[@http-equiv='last-modified']");
                if (lastmodHtmlNode != null)
                {

                    string lastmodString = lastmodHtmlNode.Attributes["content"].Value;
                    if(LessThan2Months(lastmodString))
                    {
                        //initializes an object Link to be inserted to the table
                        string title = document.DocumentNode.SelectSingleNode("//title").InnerText;
                        DateTime dateTime;
                        DateTime.TryParse(lastmodString, out dateTime);
                        AddLinkToTable(new Link(title, new Uri(url), dateTime));
                        

                        //gets a tags with valid url, adding to the queue
                        HtmlNodeCollection allATags = document.DocumentNode.SelectNodes("//a[@href]");
                        foreach (HtmlNode link in allATags)
                        {
                            string href = link.Attributes["href"].Value;
                            if(IsValidUrl(href))
                            {
                                if (!href.StartsWith("http://"))
                                {
                                    href = "http://www.cnn.com" + href;
                                }
                                if (href.StartsWith("http://www.cnn.com"))
                                {
                                    AddToQueue(href);
                                    queueSize++;
                                    totalUrlCrawled++;
                                }
                            }
                        }
                    }
                }
            }
        }


        public void AddLinkToTable(Link link)
        {
            TableOperation insertOperation = TableOperation.Insert(link);
            linkTable.Execute(insertOperation);
            tableSize++;
        }

        


        //---------------------------------------------------------------------------------------


        public void InitializeQueue(string site)
        {
            WebClient webClient = new WebClient();
            string readRobotsTxt = webClient.DownloadString(site);
            List<string> sitemapUrlList = new List<string>();
            using (StringReader stringReader = new StringReader(readRobotsTxt))
            {
                string line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    if (line.StartsWith("Sitemap:")) //4 sitemap URLs for cnn.com
                    {
                        sitemapUrlList.Add(line.Substring(9));
                    }
                    else if (line.StartsWith("Disallow:"))
                    {
                        disallowedWordSet.Add(line.Substring(10));
                    }
                }
            }
            foreach(string sitemapURL in sitemapUrlList)
            {
                if (on)
                {
                    CrawlXml(sitemapURL); //1st level
                    //return; //only doing the first sitemapURL
                }
            }
        }


        public void CrawlXml(string sitemapURL) //2nd level
        {
            XmlDocument XmlDoc = new XmlDocument();
            XmlDoc.Load(sitemapURL);
            XmlNodeList xmlNodelist;
            var abcde = XmlDoc.GetElementsByTagName("sitemap");
            if (XmlDoc.GetElementsByTagName("sitemap").Count > 0)
            {
                xmlNodelist = XmlDoc.GetElementsByTagName("sitemap");
                foreach (XmlNode xmlNode in xmlNodelist)
                {
                    if (sitemapURL.StartsWith("http://www.cnn.com"))
                    {
                        string lastmodString = xmlNode["lastmod"].InnerText;
                        if (LessThan2Months(lastmodString))
                        {
                            string locXmlString = xmlNode["loc"].InnerText;
                            if (on)
                            {
                                CrawlXml(locXmlString);
                                //return; //only the first xml
                            }
                        }
                    }
                    else
                    {
                        string locXmlString = xmlNode["loc"].InnerText;
                        if (on)
                        {
                            CrawlXml(locXmlString);
                            //return; //only the first xml
                        }
                    }
                }
            }
            else //hit last xml, contains url, process only ONCE
            {
                xmlNodelist = XmlDoc.GetElementsByTagName("url");
                foreach (XmlNode xmlNode in xmlNodelist)
                {
                    string locString = xmlNode["loc"].InnerText;
                    if (IsValidUrl(locString))
                    {
                        if (on)
                        {
                            AddToQueue(locString);
                        }
                    }
                }
            }
        }


        public Boolean LessThan2Months(string lastmodString)
        {
            DateTime lastmodDateTime;
            DateTime.TryParse(lastmodString, out lastmodDateTime);
            return DateTime.Compare(lastmodDateTime, currentDateTime) < 60;
        }


        //avoids duplicated url
        public void AddToQueue(string url)
        {
            if (!visitedUrlSet.Contains(url))
            {
                linkQueue.AddMessage(new CloudQueueMessage(url));
                queueSize++;
                totalUrlCrawled++;
                visitedUrlSet.Add(url);
            }
        }


        public Boolean IsValidUrl(string href)
        {
            if (href.EndsWith("html") || href.EndsWith("htm")
                || href.Contains(".html?") || href.Contains(".htm?"))
            {
                foreach (string disallowedWord in disallowedWordSet)
                {
                    if (href.Contains(disallowedWord))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }




        //--------------------------------------------------------------------------------------------

        public string GetAvailableMemory()
        {
            PerformanceCounter memProcess = new PerformanceCounter("Memory", "Available MBytes");
            return memProcess.NextValue().ToString() + "mbs";
        }

        public string GetCPUUtilization()
        {
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            return cpuCounter.NextValue().ToString() + "%";
        }

        public void UpdatePerformance()
        {
            performanceTable.CreateIfNotExists();
            Performance p = new Performance(GetAvailableMemory(), GetCPUUtilization(), queueSize, tableSize, totalUrlCrawled);
            TableOperation insertOperation = TableOperation.Insert(p);
            performanceTable.Execute(insertOperation);
        }

        //----------------------------------------------------------------------------------------

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }


    }
}
