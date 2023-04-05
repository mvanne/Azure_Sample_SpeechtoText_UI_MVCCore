using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Net;

namespace Speechtotext.helpers
{
    public static class Blobstorage
    {
        public static async Task<Uri?> UploadFileGenerateSAS(string SourceDocumentPath,
                                                                    string BlobStorageName,
                                                                    string BlobStorageContainerName,
                                                                    string BlobStorageEndPoint,
                                                                    string BlobStorageKey,
                                                                    double BlobSASTokenTimeoutInMinutes)
        {
            var blobUri = "https://" + BlobStorageName + BlobStorageEndPoint;
            var fileName = Path.GetFileName(SourceDocumentPath);

            // Create source storage containers
            StorageSharedKeyCredential sharedKeyCredential = new(BlobStorageName, BlobStorageKey);
            BlobServiceClient blobServiceClient = new(new Uri(blobUri), sharedKeyCredential);

            var sourceContainerClient = blobServiceClient.GetBlobContainerClient(BlobStorageContainerName);
            if (!sourceContainerClient.Exists())
            {
                sourceContainerClient = await blobServiceClient.CreateBlobContainerAsync(blobContainerName: BlobStorageContainerName, publicAccessType: PublicAccessType.BlobContainer).ConfigureAwait(false);
            }

            // Upload blob (file) to the source container
            if (!string.IsNullOrWhiteSpace(SourceDocumentPath))
            {
                BlobClient srcBlobClient = sourceContainerClient.GetBlobClient(fileName);

                using (FileStream uploadFileStream = File.OpenRead(SourceDocumentPath))
                {
                    await srcBlobClient.UploadAsync(uploadFileStream, true).ConfigureAwait(false);
                }

                //Logger.LogDebug("Request Id: [{RequestId}], Uploaded document [{srcBlobClient.Uri}] to source storage container", RequestId, srcBlobClient.Uri);

                // Generate SAS tokens for source to pass to translation service
                Uri srcSasUri = srcBlobClient.GenerateSasUri( BlobSasPermissions.List | BlobSasPermissions.Read, DateTime.UtcNow.AddMinutes(BlobSASTokenTimeoutInMinutes));

                return srcSasUri;
            }

            return null;
        }

        public static async void DeleteBlob(string SourceDocumentPath,
                                            string BlobStorageName,
                                            string BlobStorageContainerName,
                                            string BlobStorageEndPoint,
                                            string BlobStorageKey)
        {
            var blobUri = "https://" + BlobStorageName + BlobStorageEndPoint;
            var fileName = Path.GetFileName(SourceDocumentPath);

            // Create source storage containers
            StorageSharedKeyCredential sharedKeyCredential = new(BlobStorageName, BlobStorageKey);
            BlobServiceClient blobServiceClient = new(new Uri(blobUri), sharedKeyCredential);

            var sourceContainerClient = blobServiceClient.GetBlobContainerClient(BlobStorageContainerName);
            if (!sourceContainerClient.Exists())
            {
                sourceContainerClient = await blobServiceClient.CreateBlobContainerAsync(blobContainerName: BlobStorageContainerName, publicAccessType: PublicAccessType.BlobContainer).ConfigureAwait(false);
            }

            // Delete blob (file) from the source container
            if (!string.IsNullOrWhiteSpace(SourceDocumentPath))
            {
                BlobClient srcBlobClient = sourceContainerClient.GetBlobClient(fileName);
                srcBlobClient.DeleteIfExists();
            }
        }
    }
}
