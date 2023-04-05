# Azure_Sample_SpeechtoText_UI_MVCCore

This solution is built on the azure sample for Speech to text.
This is a rough example with a frontend UI that allows selecting a recording for upload and pushes the recording to blob storage.
This also returns the resulting json as a text file

The webhook is not initally needed to make this sample work but should be used in production scenarios for reliability purposes
To use this example requires provisioning speech service and blob storage
in the appsettings.json the following values need to be configured in the speechtotext project:

  "SpeechtoTextKey": "your speech service key",
  "SpeechtoTextRegion": "your speech service region",
  "SpeechtoTextEndPoint": "api.cognitive.microsoft.us", // this is the value needed for Azure Gov for comercial this needs to be updated
  "BlobStorageName": "your blob name",
  "BlobStorageContainerName": "your blob container name",
  "BlobStorageEndPoint": ".blob.core.usgovcloudapi.net",
  "BlobStorageKey": "your blob key",

If using the webhook then these values need to be configured:
  "SpeechtoTextKey": "your speech key",
  "SpeechtoTextRegion": "your speech region",
  "SpeechtoTextEndPoint": "api.cognitive.microsoft.us",
  
  
