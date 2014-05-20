using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for WebService1
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class WebService1 : System.Web.Services.WebService
    {
        public static string availableMemory = "";
        public static string cpuUsage = "";
        public static string queueSize = "";
        public static string tableSize = "";
        public static string totalUrlCrawled = "";


        [WebMethod]
        public void Start()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue adminQueue = queueClient.GetQueueReference("adminqueue");
            adminQueue.CreateIfNotExists();
            adminQueue.AddMessage(new CloudQueueMessage("start"));
        }

        [WebMethod]
        public void Stop()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue adminQueue = queueClient.GetQueueReference("adminqueue");
            adminQueue.CreateIfNotExists();
            adminQueue.AddMessage(new CloudQueueMessage("stop"));
        }


        //does
        [WebMethod]
        public void Clear()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue adminQueue = queueClient.GetQueueReference("linkqueue");
            adminQueue.CreateIfNotExists();
            adminQueue.Clear();

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable linkTable = tableClient.GetTableReference("linktable");
            linkTable.DeleteIfExists();
            linkTable.CreateIfNotExists();
        }

        //TableQuery<Link> query = new TableQuery<Link>().Select(new string[] { "ram" });


        [WebMethod]
        public string SearchTable(string url)
        {
            if (url != null)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                    ConfigurationManager.AppSettings["StorageConnectionString"]);
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                CloudTable table = tableClient.GetTableReference("linktable");
                table.CreateIfNotExists();

                Uri uri = new Uri(url);
                if (uri != null)
                {
                    string partitionKey = uri.Host;
                    string rowKey = EncodeUrlInKey(uri.AbsolutePath);
                    TableOperation target = TableOperation.Retrieve<Link>(partitionKey, rowKey);
                    Link result = (Link)table.Execute(target).Result;
                    if (result != null)
                    {
                        return result.Title;
                    }
                }
            }
            return "no result found!";
        }

        private static String EncodeUrlInKey(String url)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(url);
            var base64 = System.Convert.ToBase64String(keyBytes);
            return base64.Replace('/', '_');
        }





        [WebMethod]
        public void Refresh()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue adminQueue = queueClient.GetQueueReference("adminqueue");
            adminQueue.CreateIfNotExists();
            adminQueue.AddMessage(new CloudQueueMessage("refresh"));

            Thread.Sleep(1000);

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("performancetable");
            TableOperation target = TableOperation.Retrieve<Performance>("uniquePK", "uniqueRK");
            Performance result = (Performance)table.Execute(target).Result;
            if(result != null)
            {
                availableMemory = result.AvailableMemory;
                cpuUsage = result.CpuUsage;
                queueSize = result.QueueSize.ToString();
                tableSize = result.TableSize.ToString();
                totalUrlCrawled = result.TotalUrlCrawled.ToString();
                table.Delete();
            }
        }

        [WebMethod]
        public string GetAvailableMemory()
        {
            return availableMemory;
        }

        [WebMethod]
        public string GetCPU()
        {
            return cpuUsage;
        }

        [WebMethod]
        public string GetQueueSize()
        {
            return queueSize;
        }

        [WebMethod]
        public string GetTableSize()
        {
            return tableSize;
        }

        [WebMethod]
        public string GetTotalUrlCrawled()
        {
            return totalUrlCrawled;
        }

    }
}
