using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    class Performance : TableEntity
    {

        public Performance(string availableMemory, string cpuUsage, int queueSize, int tableSize, int totalUrlCrawled)
        {
            this.PartitionKey = "uniquePK";
            this.RowKey = "uniqueRK";
            this.AvailableMemory = availableMemory;
            this.CpuUsage = cpuUsage;
            this.QueueSize = queueSize;
            this.TableSize = tableSize;
            this.TotalUrlCrawled = totalUrlCrawled;
        }

        public Performance() { }
        public string AvailableMemory { get; set; }
        public string CpuUsage { get; set; }
        public int QueueSize { get; set; }
        public int TableSize { get; set; }
        public int TotalUrlCrawled { get; set; }


    }
}
