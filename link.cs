using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    public class Link : TableEntity
    {
        public Link(string title, Uri address, DateTime dateTime)
        {
            this.PartitionKey = address.Host;
            this.RowKey = EncodeUrlInKey(address.AbsolutePath);
            this.Title = title;
            this.Address = address;
            this.dateTime = dateTime;
        }

        public Link() { }
        public string Title { get; set; }
        public Uri Address { get; set; }
        public DateTime dateTime { get; set; }

        private static String EncodeUrlInKey(String url)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(url);
            var base64 = System.Convert.ToBase64String(keyBytes);
            return base64.Replace('/', '_');
        }
    }
}
