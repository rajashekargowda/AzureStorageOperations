using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AzureStorageOperations.Models
{
    public class FileUpload
    {
        public string FileName { get; set; }

        public string FilePath { get; set; }
        public Uri FileURI { get; set; }

        public List<FileUpload> FileUploadList { get; set; }
    }//sdfbgffasdfsdfds
    internal class FileBlock
    {
        public string Id
        {
            get;
            set;
        }

        public byte[] Content
        {
            get;
            set;
        }
    }


}