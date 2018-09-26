using AzureStorageOperations.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;

namespace AzureStorageOperations.Controllers
{
    public class BlobOperationsController : Controller
    {
        static string StorageAccountName = "tcoblobuat";
        static string StorageAccountKey = "ZbdL+JZKXLUFm3VX8lR4MVA/UhR0M9z2GJElVBsxSNLMAt4nvBBwqKsurzoB9Ij6nL5zULpUsxH61OVZfeXXHQ==";
        static string ContainerName = "telematics-ecrb";
       
        // GET: BlobOperations
        public async Task<ActionResult> BlobOperaionsView()
        {
            
            FileUpload fileobj = new FileUpload();
            fileobj = await ListContainersAsyncREST(StorageAccountName, StorageAccountKey, CancellationToken.None);
            return View(fileobj);
        }


        public async Task DownloadBlob(string fileName)
        {

            string sContetType = string.Empty;
            string sContentLength = string.Empty;

            MemoryStream memStream = new MemoryStream();           

            String uri = string.Format("http://{0}.blob.core.windows.net/{1}/{2}", StorageAccountName, ContainerName,fileName);

            // Set this to whatever payload you desire. Ours is null because 
            //   we're not passing anything in.
            Byte[] requestPayload = null;

            //Instantiate the request message with a null payload.
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
            { Content = (requestPayload == null) ? null : new ByteArrayContent(requestPayload) })
            {

                // Add the request headers for x-ms-date and x-ms-version.
                DateTime now = DateTime.UtcNow;
                httpRequestMessage.Headers.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
                httpRequestMessage.Headers.Add("x-ms-version", "2017-04-17");
                

                // Add the authorization header.
                httpRequestMessage.Headers.Authorization = AzureStorageAuthenticatorHelper.GetAuthorizationHeader(
                   StorageAccountName, StorageAccountKey, now, httpRequestMessage);
                

                // Send the request.
                using (HttpResponseMessage httpResponseMessage = await new HttpClient().SendAsync(httpRequestMessage, CancellationToken.None))
                {
                    // If successful (status code = 200), 
                    //   parse the XML response for the container names.
                    if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        String xmlString = await httpResponseMessage.Content.ReadAsStringAsync();
                        var mstream = await httpResponseMessage.Content.ReadAsStreamAsync();
                        mstream.CopyTo(memStream);
                        sContetType = httpResponseMessage.Content.Headers.ContentType.MediaType;                        
                        sContentLength =  httpResponseMessage.Content.Headers.ContentLength.ToString();                        
                    }
                }
            }

            Response.ContentType = sContetType;
            Response.AddHeader("Content-Disposition", "Attachment;filename=" + fileName);
            Response.AddHeader("Content-Length", sContentLength);
            Response.BinaryWrite(memStream.ToArray());            
        }

      
        private  async Task<FileUpload> ListContainersAsyncREST(string storageAccountName, string storageAccountKey, CancellationToken cancellationToken)
        {

            FileUpload fileobj = new FileUpload();
            List<FileUpload> filelist = new List<FileUpload>();
            // Construct the URI. This will look like this:
            //   https://myaccount.blob.core.windows.net/resource
            String uri = string.Format("http://{0}.blob.core.windows.net/{1}?restype=container&comp=list", storageAccountName, ContainerName);

            // Set this to whatever payload you desire. Ours is null because 
            //   we're not passing anything in.
            Byte[] requestPayload = null;

            //Instantiate the request message with a null payload.
            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
            { Content = (requestPayload == null) ? null : new ByteArrayContent(requestPayload) })
            {

                // Add the request headers for x-ms-date and x-ms-version.
                DateTime now = DateTime.UtcNow;
                httpRequestMessage.Headers.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
                httpRequestMessage.Headers.Add("x-ms-version", "2017-04-17");
                // If you need any additional headers, add them here before creating
                //   the authorization header. 

                // Add the authorization header.
                httpRequestMessage.Headers.Authorization = AzureStorageAuthenticatorHelper.GetAuthorizationHeader(
                   storageAccountName, storageAccountKey, now, httpRequestMessage);

                // Send the request.
                using (HttpResponseMessage httpResponseMessage = await new HttpClient().SendAsync(httpRequestMessage, cancellationToken))
                {
                    // If successful (status code = 200), 
                    //   parse the XML response for the container names.
                    if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
                    {
                        String xmlString = await httpResponseMessage.Content.ReadAsStringAsync();
                        //var stream = await httpResponseMessage.Content.ReadAsStreamAsync();



                        XElement x = XElement.Parse(xmlString);
                        //foreach (XElement container in x.Element("Containers").Elements("Container"))
                        //{
                        //    Console.WriteLine("Container name = {0}", container.Element("Name").Value);
                        //}
                        foreach (XElement container in x.Element("Blobs").Elements("Blob"))
                        {
                            FileUpload obj = new FileUpload();
                            obj.FileName = container.Element("Name").Value;
                            filelist.Add(obj);
                            //Console.WriteLine("Blob name = {0}", container.Element("Name").Value);
                        }
                    }
                }
            }
            fileobj.FileUploadList = filelist;
            return fileobj;
        }


    }
}