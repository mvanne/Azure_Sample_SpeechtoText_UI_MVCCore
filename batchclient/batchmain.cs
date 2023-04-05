//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

namespace BatchClient
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection.Metadata;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class batchmain
    {
        // Replace with your subscription key
        private static string SubscriptionKey = string.Empty;

        // Update with your service region
        private static string Region = string.Empty;

        // Update with your Cloud Endpoint
        private static string EndPoint = string.Empty;

        // replace with your app service name (check publish on webhook receiver project)
        public const string WebHookAppServiceName = "cbospeechwebhookfunction";

        // replace with a secure secret (used for hashing)
        public const string WebHookSecret = "somethingverysecretisbesthere";

        // recordings and locale
        private const string Locale = "en-US";

        // **** put in github avn2006@gmail.com **** 

        //private static Uri ContentAzureBlobContainer = new Uri("<SAS URI pointing to an container in Azure Blob Storage>");
        private static Uri WebHookCallbackUrl = new Uri($"https://{WebHookAppServiceName}.azurewebsites.us/api/callback");

        // For use of custom trained model:
        private static EntityReference CustomModel = null;
        //private static EntityReference CustomModel =
        //    new EntityReference { Self = new Uri($"https://{Region}.api.cognitive.microsoft.com/speechtotext/v3.0/models/<id of custom model>")};

        private const string DisplayName = "Simple transcription";

        public static async Task<RecognitionResults?> TranscribeMainAsync(string subscriptionKey, string region, string endpoint, Uri recordingsBlobUri)
        {
            SubscriptionKey = subscriptionKey;
            Region = region;
            EndPoint = endpoint;

            // create the client object and authenticate
            using (var client = BatchClient.CreateApiV3Client(SubscriptionKey, $"{Region}.{EndPoint}"))
            {
                // uncomment next line when using web hooks
                //await SetupWebHookAsync(client).ConfigureAwait(false);

                return await TranscribeAsync(client, recordingsBlobUri).ConfigureAwait(false);

                // uncomment next line when using web hooks
                //await DeleteAllWebHooksAsync(client).ConfigureAwait(false);
            }
        }

        private async static Task<RecognitionResults?> TranscribeAsync(BatchClient client, Uri recordingsBlobUri)
        {
            Console.WriteLine("Deleting all existing completed transcriptions.");

            // get all transcriptions for the subscription
            PaginatedTranscriptions paginatedTranscriptions = null;
            do
            {
                if (paginatedTranscriptions == null)
                {
                    paginatedTranscriptions = await client.GetTranscriptionsAsync().ConfigureAwait(false);
                }
                else
                {
                    paginatedTranscriptions = await client.GetTranscriptionsAsync(paginatedTranscriptions.NextLink).ConfigureAwait(false);
                }

                // delete all pre-existing completed transcriptions. If transcriptions are still running or not started, they will not be deleted
                foreach (var transcriptionToDelete in paginatedTranscriptions.Values)
                {
                    // delete a transcription
                    await client.DeleteTranscriptionAsync(transcriptionToDelete.Self).ConfigureAwait(false);
                    Console.WriteLine($"Deleted transcription {transcriptionToDelete.Self}");
                }
            }
            while (paginatedTranscriptions.NextLink != null);

            // <transcriptiondefinition>
            var newTranscription = new Transcription
            {
                DisplayName = DisplayName, 
                Locale = Locale, 
                ContentUrls = new[] { recordingsBlobUri },
                //ContentContainerUrl = ContentAzureBlobContainer,
                Model = CustomModel,
                Properties = new TranscriptionProperties
                {
                    TimeToLive = TimeSpan.FromDays(1),
                    IsWordLevelTimestampsEnabled = true,
                    IsDisplayFormWordLevelTimestampsEnabled = false,

                    // uncomment the following block to enable and configure speaker separation
                    IsDiarizationEnabled = true,
                    Diarization = new DiarizationProperties
                    {
                        Speakers = new DiarizationSpeakersProperties
                        {
                            MinCount = 1,
                            MaxCount = 5
                        }
                    },

                    // uncomment the following block to enable and configure language identification prior to transcription
                    // LanguageIdentification = new LanguageIdentificationProperties
                    // {
                    //     CandidateLocales = new CultureInfo[] { new CultureInfo("en-US"), new CultureInfo("ja-JP") },
                    // }
                }
            };

            newTranscription = await client.CreateTranscriptionAsync(newTranscription).ConfigureAwait(false);
            Console.WriteLine($"Created transcription {newTranscription.Self}");
            // </transcriptiondefinition>

            // get the transcription Id from the location URI
            var createdTranscriptions = new List<Uri> { newTranscription.Self };

            Console.WriteLine("Checking status.");

            // get the status of our transcriptions periodically and log results
            int completed = 0, running = 0, notStarted = 0;
            while (completed < 1)
            {
                completed = 0; running = 0; notStarted = 0;

                // get all transcriptions for the user
                paginatedTranscriptions = null;
                do
                {
                    // <transcriptionstatus>
                    if (paginatedTranscriptions == null)
                    {
                        paginatedTranscriptions = await client.GetTranscriptionsAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        paginatedTranscriptions = await client.GetTranscriptionsAsync(paginatedTranscriptions.NextLink).ConfigureAwait(false);
                    }

                    // get the status of all transcriptions. If transcriptions are still running or not started, then wait
                    foreach (var transcription in paginatedTranscriptions.Values)
                    {
                        switch (transcription.Status)
                        {
                            case "Failed":
                            case "Succeeded":
                                // we check to see if it was one of the transcriptions we created from this client.
                                if (!createdTranscriptions.Contains(transcription.Self))
                                {
                                    // not created form here, continue
                                    continue;
                                }

                                completed++;

                                // if the transcription was successful, check the results
                                if (transcription.Status == "Succeeded")
                                {
                                    Console.WriteLine("Transcription succeeded. Results: ");

                                    var paginatedfiles = await client.GetTranscriptionFilesAsync(transcription.Links.Files).ConfigureAwait(false);

                                    var resultFile = paginatedfiles.Values.FirstOrDefault(f => f.Kind == ArtifactKind.Transcription);
                                    var result = await client.GetTranscriptionResultAsync(new Uri(resultFile.Links.ContentUrl)).ConfigureAwait(false);
                                    return result;
                                    //var resultfile = JsonConvert.SerializeObject(result, SpeechJsonContractResolver.WriterSettings);
                                    //Console.WriteLine(resultfile);


                                }
                                else
                                {
                                    Console.WriteLine("Transcription failed. Status: {0}", transcription.Properties.Error.Message);
                                    return null;
                                }

                                //break;

                            case "Running":
                                running++;
                                break;

                            case "NotStarted":
                                notStarted++;
                                break;
                        }
                    }

                    // for each transcription in the list we check the status
                    Console.WriteLine(string.Format("Transcriptions status: {0} completed, {1} running, {2} not started yet", completed, running, notStarted));
                }
                while (paginatedTranscriptions.NextLink != null);

                // </transcriptionstatus>
                // check again after 1 minute
                await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);
            }

            //Console.WriteLine("Press any key...");
            //Console.ReadKey();
            return null;
        }

        private async static Task SetupWebHookAsync(BatchClient client)
        {
            await DeleteAllWebHooksAsync(client).ConfigureAwait(false);

            var webHook = new WebHook
            {
                DisplayName = "Transcription web hook",
                Events = new Dictionary<WebHookEventKind, bool>
                {
                    [WebHookEventKind.TranscriptionCreation] = true,
                    [WebHookEventKind.TranscriptionProcessing] = true,
                    [WebHookEventKind.TranscriptionCompletion] = true
                },
                WebUrl = WebHookCallbackUrl,
                Properties = new WebHookProperties
                {
                    Secret = WebHookSecret
                }
            };

            webHook = await client.CreateWebHookAsync(webHook);

            Console.WriteLine(string.Format("Created webHook {0}.", webHook.Self));
        }

        private static async Task DeleteAllWebHooksAsync(BatchClient client)
        {
            // get all web hooks for the subscription
            PaginatedWebHooks paginatedWebHooks = null;
            do
            {
                if (paginatedWebHooks == null)
                {
                    paginatedWebHooks = await client.GetWebHooksAsync().ConfigureAwait(false);
                }
                else
                {
                    paginatedWebHooks = await client.GetWebHooksAsync(paginatedWebHooks.NextLink).ConfigureAwait(false);
                }

                // delete all pre-existing web hooks.
                foreach (var webHooksToDelete in paginatedWebHooks.Values)
                {
                    // delete web hook
                    await client.DeleteWebHookAsync(webHooksToDelete.Self).ConfigureAwait(false);
                    Console.WriteLine($"Deleted web hook {webHooksToDelete.Self}");
                }
            }
            while (paginatedWebHooks.NextLink != null);
        }
    }
}