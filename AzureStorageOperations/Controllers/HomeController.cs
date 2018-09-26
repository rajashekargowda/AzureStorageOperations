using AzureStorageOperations.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure; // Namespace for CloudConfigurationManager
using Microsoft.WindowsAzure.Storage; // Namespace for CloudStorageAccount
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure;
using System.Configuration;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System.Net;
using System.Globalization;



namespace AzureStorageOperations.Controllers
{
    public class HomeController : Controller
    {
        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
    CloudConfigurationManager.GetSetting("StorageConnectionString"));
        private string sContainerName = "test1";

        private const int MaxBlockSize = 4000000;

        // GET: Home
        public async Task<ActionResult> Index()
        {
            
            FileUpload fileobj = new FileUpload();

            fileobj = await GetAllBlobsFromAzureStorage();
            return View(fileobj);
        }

        

        public CloudBlobContainer StorageConnectionInstance()
        {
            CloudStorageAccount storageAccountObj = CloudStorageAccount.Parse(
    CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(sContainerName);
            return container;
        }

        public async Task<FileUpload> GetAllBlobsFromAzureStorage()
        {
            FileUpload fileobj = new FileUpload();

            CloudBlobContainer container = StorageConnectionInstance();
            //ListAllBlobs();
            fileobj.FileUploadList = await ListBlobsSegmentedInFlatListing(container);
            return fileobj;
        }

        [HttpPost]
        public async Task<ActionResult> Index(HttpPostedFileBase file_Uploader)
        {
            if (file_Uploader != null)
            {
                //UploadAndGetBlobSasUri(StorageConnectionInstance());
                await uploadFilestoBlobAsync(file_Uploader);
                FileUpload fileobj = new FileUpload();
                fileobj = await GetAllBlobsFromAzureStorage();
                return View(fileobj);              
            }
            return View();
        }
        
        private IEnumerable<FileBlock> GetFileBlocks(byte[] fileContent)
        {
            HashSet<FileBlock> hashSet = new HashSet<FileBlock>();
            if (fileContent.Length == 0)
                return new HashSet<FileBlock>();

            int blockId = 0;
            int ix = 0;

            int currentBlockSize = MaxBlockSize;

            while (currentBlockSize == MaxBlockSize)
            {
                if ((ix + currentBlockSize) > fileContent.Length)
                    currentBlockSize = fileContent.Length - ix;

                byte[] chunk = new byte[currentBlockSize];
                Array.Copy(fileContent, ix, chunk, 0, currentBlockSize);

                hashSet.Add(
                    new FileBlock()
                    {
                        Content = chunk,
                        Id = Convert.ToBase64String(System.BitConverter.GetBytes(blockId))
                    });

                ix += currentBlockSize;
                blockId++;
            }

            return hashSet;
        }

        public async Task UploadLargeFilesInChunks(HttpPostedFileBase file_Uploader)
        {           
            CloudBlobContainer container = StorageConnectionInstance();
            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(file_Uploader.FileName);

            blockBlob.StreamWriteSizeInBytes = 1048576;
            blockBlob.StreamMinimumReadSizeInBytes = 1048576;

            //set the blob upload timeout and retry strategy
            BlobRequestOptions options = new BlobRequestOptions();
            options.ServerTimeout = new TimeSpan(0, 180, 0);
            options.RetryPolicy = new ExponentialRetry(TimeSpan.Zero, 20);
            byte[] fileData = null;
            using (var binaryReader = new BinaryReader(Request.Files[0].InputStream))
            {
                fileData = binaryReader.ReadBytes(Request.Files[0].ContentLength);
            }


            HashSet<string> blocklist = new HashSet<string>();
            List<FileBlock> bloksT = GetFileBlocks(fileData).ToList();

            foreach (FileBlock block in GetFileBlocks(fileData))
            {
                await blockBlob.PutBlockAsync(
                    block.Id,
                    new MemoryStream(block.Content, true), null,
                    null, options, null
                    );

                blocklist.Add(block.Id);
            }

            await blockBlob.PutBlockListAsync(blocklist, null, options, null);

            //set the status of operation of blob upload as succeeded as there is not exception
           var blockuri = blockBlob.Uri;
            var bloackname = blockBlob.Name;
            
            
        }
        public static string GetMD5HashFromStream(Stream stream)
        {

            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(stream);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }
        public bool CreateContainer()
        {
            

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve a reference to a container.
            CloudBlobContainer container = StorageConnectionInstance();

            // Create the container if it doesn't already exist.
            container.CreateIfNotExists();
            container.SetPermissions(
    new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

            return true;
        }

        public async Task uploadFilestoBlobAsync(HttpPostedFileBase file_Uploader)
        {
            //// Retrieve storage account from connection string.
            //CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            //    CloudConfigurationManager.GetSetting("StorageConnectionString"));

          

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = StorageConnectionInstance();

            // Retrieve reference to a blob named "myblob".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(file_Uploader.FileName);
            blockBlob.Properties.ContentType = file_Uploader.ContentType;

            byte[] fileData = null;
            using (var binaryReader = new BinaryReader(file_Uploader.InputStream))
            {
                fileData = binaryReader.ReadBytes(file_Uploader.ContentLength);
            }

            await blockBlob.UploadFromStreamAsync(file_Uploader.InputStream);           
           // return true;
        }

        public bool ListAllBlobs()
        {
    //        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
    //CloudConfigurationManager.GetSetting("StorageConnectionString"));

            
            CloudBlobContainer container = StorageConnectionInstance();
           var res = container.ListBlobs(null, false);
            // Loop over items within the container and output the length and URI.
            foreach (IListBlobItem item in container.ListBlobs(null, false))
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    CloudBlockBlob blob = (CloudBlockBlob)item;

                    Console.WriteLine("Block blob of length {0}: {1}", blob.Properties.Length, blob.Uri);

                }
                else if (item.GetType() == typeof(CloudPageBlob))
                {
                    CloudPageBlob pageBlob = (CloudPageBlob)item;

                    Console.WriteLine("Page blob of length {0}: {1}", pageBlob.Properties.Length, pageBlob.Uri);

                }
                else if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    CloudBlobDirectory directory = (CloudBlobDirectory)item;

                    Console.WriteLine("Directory: {0}", directory.Uri);
                }
            }
            return true;
        }

        async public  Task<List<FileUpload>> ListBlobsSegmentedInFlatListing(CloudBlobContainer container)
        {
            List<FileUpload> filelist = new List<FileUpload>();
            try
            {
                //List blobs to the console window, with paging.
                //Console.WriteLine("List blobs in pages:");

                int i = 0;
                BlobContinuationToken continuationToken = null;
                BlobResultSegment resultSegment = null;
                

                //Call ListBlobsSegmentedAsync and enumerate the result segment returned, while the continuation token is non-null.
                //When the continuation token is null, the last page has been returned and execution can exit the loop.
                do
                {
                    //This overload allows control of the page size. You can return all remaining results by passing null for the maxResults parameter,
                    //or by calling a different overload.
                    resultSegment = await container.ListBlobsSegmentedAsync("", true, BlobListingDetails.All, 10, continuationToken, null, null);
                    if (resultSegment.Results.Count<IListBlobItem>() > 0)
                    {
                        
                        Console.WriteLine("Page {0}:", ++i);    
                    }
                    foreach (var blobItem in resultSegment.Results)
                    {
                        FileUpload obj = new FileUpload();
                        obj.FileURI = blobItem.StorageUri.PrimaryUri;
                        obj.FileName = ((Microsoft.WindowsAzure.Storage.Blob.CloudBlob)blobItem).Name;
                        filelist.Add(obj);
                        //Console.WriteLine("\t{0}", blobItem.StorageUri.PrimaryUri);
                    }
                    //Console.WriteLine();
                    Session["fileUploader"] = filelist;
                    //Get the continuation token.
                    continuationToken = resultSegment.ContinuationToken;
                }
                while (continuationToken != null);
            }
            catch(Exception ex)
            {
                throw ex;
            }
            return filelist;
        }
     
        public void DownloadBlob(string fileName)
        {
            
            // Retrieve reference to a previously created container.
            CloudBlobContainer container = StorageConnectionInstance();

            // Retrieve reference to a blob named "photo1.jpg".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
            MemoryStream memStream = new MemoryStream();
            //blockBlob.DownloadToStream(memStream);
            
            
            blockBlob.DownloadToStream(memStream);
            Response.ContentType = blockBlob.Properties.ContentType;
            Response.AddHeader("Content-Disposition", "Attachment;filename=" + fileName);
            Response.AddHeader("Content-Length", blockBlob.Properties.Length.ToString());
            Response.BinaryWrite(memStream.ToArray());
            //return File(memStream, blockBlob.Properties.ContentType, fileName);
           
           // return File(memStream, blockBlob.Properties.ContentType, fileName);
        }


        public async Task<ActionResult> RemoveUploadFile(string fileName)
        {            
            CloudBlobContainer container = StorageConnectionInstance();
            // Retrieve reference to a blob named "myblob.txt".
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
            // Delete the blob.
            await blockBlob.DeleteAsync();
            return RedirectToAction("Index", "Home");           
        }

        public string UploadAndGetBlobSasUri(CloudBlobContainer container)
        {
            //Get a reference to a blob within the container.
            CloudBlockBlob blob = container.GetBlockBlobReference("sasblob.txt");

            //Upload text to the blob. If the blob does not yet exist, it will be created.
            //If the blob does exist, its existing content will be overwritten.
            string blobContent = "This blob will be accessible to clients via a shared access signature (SAS).";
            blob.UploadText(blobContent);

            //Set the expiry time and permissions for the blob.
            //In this case, the start time is specified as a few minutes in the past, to mitigate clock skew.
            //The shared access signature will be valid immediately.
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-5);
            sasConstraints.SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddHours(24);
            sasConstraints.Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write;

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            string sasBlobToken = blob.GetSharedAccessSignature(sasConstraints);

            //Return the URI string for the container, including the SAS token.
            return blob.Uri + sasBlobToken;
        }

    }
}
