using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace sbc_scrape
{
	class GoogleDrive
	{
		private DriveService service;
		public GoogleDrive(string applicationName = "sbc-data-access", string credentialsFilepath = @"credentials.json", string[] scopes = null)
		{
			if (scopes == null)
				scopes = new string[] { DriveService.Scope.Drive }; //DriveReadonly

			Google.Apis.Http.IConfigurableHttpClientInitializer credential;
			var parsedCredentialsFile = JObject.Parse(File.ReadAllText(credentialsFilepath));
			var credType = parsedCredentialsFile.SelectToken("$.type")?.Value<string>();
			if (credType == "service_client")
			{
				credential = GoogleCredential.FromFile(credentialsFilepath).CreateScoped(scopes);
			}
			//else if (parsedCredentialsFile.SelectToken("$.web") != null)
			else if (parsedCredentialsFile.SelectToken("$.installed") != null)
			{
				using (var stream =
					new FileStream(credentialsFilepath, FileMode.Open, FileAccess.Read))
				{
					// The file token.json stores the user's access and refresh tokens, and is created automatically when the authorization flow completes for the first time.
					string credPath = "token.json";
					credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
						GoogleClientSecrets.Load(stream).Secrets, scopes, "user", CancellationToken.None, new FileDataStore(credPath, true)).Result;
				}
			}
			else
				throw new ArgumentException("Unknown type of credentials");

			service = new DriveService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = applicationName,
			});
		}

		public List<Google.Apis.Drive.v3.Data.File> GetFiles()
		{
			var listRequest = service.Files.List();
			listRequest.PageSize = 100;
			//https://developers.google.com/drive/api/v3/performance#partial-response
			listRequest.Fields = "nextPageToken, files(id, name, parents, mimeType)";
			//https://developers.google.com/drive/api/v3/search-files
			listRequest.Q = "'me' in owners and trashed=false and !sharedWithMe";
			//listRequest.Q = "mimeType='application/vnd.google-apps.folder'";
			//https://developers.google.com/drive/api/v3/search-shareddrives

			var result = new List<Google.Apis.Drive.v3.Data.File>();
			while (true)
			{
				var found = listRequest.Execute();
				if (found.Files != null)
					result.AddRange(found.Files);
				if (found.NextPageToken == null)
					break;
				listRequest.PageToken = found.NextPageToken;
			}
			return result;
		}

		public Google.Apis.Drive.v3.Data.File UploadFile(string uploadFile,
			IEnumerable<string> parentIds = null, string descrp = "Uploaded with .NET!", string contentType = "application/json")
		{
			var body = new Google.Apis.Drive.v3.Data.File();
			body.Name = Path.GetFileName(uploadFile);
			body.Description = descrp;
			body.MimeType = contentType;
			body.Parents = parentIds?.ToList() ?? new List<string>();

			using (var stream =	new FileStream(uploadFile, FileMode.Open, FileAccess.Read))
			{
				var request = service.Files.Create(body, stream, contentType);
				request.Fields = "id, name, parents"; //mimeType
				request.UseContentAsIndexableText = true;

				var progress = request.Upload();
				//Not sure if needed, but return value indicates it...:
				while (progress.Status != Google.Apis.Upload.UploadStatus.Completed
					&& progress.Status != Google.Apis.Upload.UploadStatus.Failed)
				{
					Thread.Sleep(100);
				}

				if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
				{
					throw progress.Exception;
				}
				return request.ResponseBody;
			}
		}
	}
}
