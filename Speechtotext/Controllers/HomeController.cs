using Microsoft.AspNetCore.Mvc;
using Speechtotext.Models;
using System.Diagnostics;
using BatchClient;
using MimeKit;
using Speechtotext.helpers;
using Newtonsoft.Json;
using System.Text;

namespace Speechtotext.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration Configuration;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _logger = logger;
            Configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TranslateFile(IFormFile FormFile, string TargetLanguage)
        {
            try
            {
                var resquestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
                //var currentUser = HttpContext.User.Identity?.Name ?? string.Empty;

                var subscriptionKey = Configuration["SpeechtoTextKey"];
                var region = Configuration["SpeechtoTextRegion"];
                var endpoint = Configuration["SpeechtoTextEndPoint"];
                var blobStorageName = Configuration["BlobStorageName"];
                var blobStorageContainerName = Configuration["BlobStorageContainerName"];
                var blobStorageEndPoint = Configuration["BlobStorageEndPoint"];
                var blobStorageKey = Configuration["BlobStorageKey"];
                var blobSASTokenTimeoutInMinutes = Convert.ToDouble(Configuration["BlobSASTokenTimeoutInMinutes"]);

                //if ((FormFile != null) && (FormFile.Length > 0) && (!String.IsNullOrEmpty(TargetLanguage)))
                if ((FormFile != null) && (FormFile.Length > 0))
                    {
                    var fileName = FormFile.FileName;
                    var filePath = Path.Combine(Path.GetTempPath(), fileName);
                    //var langaugeKey = _languages[TargetLanguage];

                    //_logger.LogInformation("Initiated Translation: RequestId = [{resquestId}], User = [{currentUser}], Source file = [{filePath}], Target language = [{TargetLanguage}], Target language key = [{langaugeKey}]", resquestId, currentUser, filePath, TargetLanguage, langaugeKey);

                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await FormFile.CopyToAsync(stream);
                    }

                    var recordingsBlobUri = Blobstorage.UploadFileGenerateSAS(filePath, blobStorageName, blobStorageContainerName, blobStorageEndPoint, blobStorageKey, blobSASTokenTimeoutInMinutes).Result;

                    if (recordingsBlobUri != null)
                    {
                        var textResult = batchmain.TranscribeMainAsync(subscriptionKey, region, endpoint, recordingsBlobUri).Result;

                        //delete blob from storage
                        Blobstorage.DeleteBlob(filePath, blobStorageName, blobStorageContainerName, blobStorageEndPoint, blobStorageKey);

                        if (textResult != null)
                        {


                            var resultfile = JsonConvert.SerializeObject(textResult, SpeechJsonContractResolver.WriterSettings);


                            return File(Encoding.UTF8.GetBytes(resultfile), "text/plain", string.Format("{0}.txt", "test"));



                            // a way of templatizing the results and dynamically loading into a word document
                            //https://www.grapecity.com/documents-api-word/docs/online/json_data_source_report_templates.html

                            //GcWordDocument doc = new GcWordDocument();
                            //doc.Load(DocOutputTemplateFile);
                            //doc.DataTemplate.DataSources.Add("ds", resultfile);
                            //doc.DataTemplate.Process();
                            //doc.Save(DocOutputLocation);
                            //Word.Application app = new Word.Application();
                            //Word.Document doc = app.Documents.Add();
                            //Word.Selection currentSelection = app.Selection;
                            //currentSelection.TypeText(result.CombinedRecognizedPhrases.First().Display);
                            ////app.Documents[DocOutputLocation].Save();
                            //app.Documents.Save();

                            //TranslateFile tf = new();
                            //var downloadFilePath = await tf.TranslationWithAzureBlob(cogDocumentTranslationEndpoint,
                            //                                                         cogDocumentTranslationApiKey,
                            //                                                         blobStorageName,
                            //                                                         blobStorageEndPoint,
                            //                                                         blobStorageKey,
                            //                                                         blobSASTokenTimeoutInMinutes,
                            //                                                         filePath,
                            //                                                         langaugeKey,
                            //                                                         resquestId,
                            //                                                         _logger);

                            //_logger.LogInformation("Downloaded Translation From Blob: RequestId = [{resquestId}], User = [{currentUser}], Translated file = [{downloadFilePath}]", resquestId, currentUser, downloadFilePath);

                            //var currentFileName = Path.GetFileName(downloadFilePath);
                            //var fs = new FileStream(downloadFilePath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose);

                            //System.Net.Mime.ContentDisposition cd = new System.Net.Mime.ContentDisposition
                            //{
                            //    FileName = currentFileName,
                            //    Inline = true,  // false = prompt the user for downloading;  true = browser to try to show the file inline
                            //};

                            //Response.Headers.Add("Content-Disposition", cd.ToString());
                            //Response.Headers.Add("Content-Length", fs.Length.ToString());

                            //_logger.LogInformation("Downloading Translation To Client: RequestId = [{resquestId}], User = [{currentUser}], Source file = [{filePath}], Target language = [{TargetLanguage}], Target language key = [{langaugeKey}]", resquestId, currentUser, filePath, TargetLanguage, langaugeKey);

                            //return File(fs, MimeTypes.GetMimeType(downloadFilePath), currentFileName); // the local copy is deleted on successful download
                        }
                    }
                }
            }
            catch
            {
                return RedirectToAction("Error", new { message = "Failed to translate" });
            }

            return RedirectToAction("Error", new { message = "Missing Input, Please select a file and a language." });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}