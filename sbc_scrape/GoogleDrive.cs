using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
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
		static string[] Scopes = { DriveService.Scope.Drive }; //DriveReadonly

		public static void Test(string applicationName = "sbc-data-access", string credentialsFilepath = @"credentials.json")
		{
			//UserCredential credential;
			//using (var stream =
			//	new FileStream(credentialsFilepath, FileMode.Open, FileAccess.Read))
			//{
			//	// The file token.json stores the user's access and refresh tokens, and is created automatically when the authorization flow completes for the first time.
			//	string credPath = "token.json";
			//	credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
			//		GoogleClientSecrets.Load(stream).Secrets, Scopes, "user", CancellationToken.None, new FileDataStore(credPath, true)).Result;
			//}

			var credential = GoogleCredential.FromFile(credentialsFilepath).CreateScoped(Scopes);

			var service = new DriveService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = applicationName,
			});

			//var uploaded = UploadFile(service, @"", new string[] { });
			var files = GetFiles(service);
		}

		public static List<Google.Apis.Drive.v3.Data.File> GetFiles(DriveService service)
		{
			var listRequest = service.Files.List();
			listRequest.PageSize = 100;
			listRequest.Fields = "nextPageToken, files(id, name, parents, mimeType)";
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

		public static Google.Apis.Drive.v3.Data.File UploadFile(DriveService service, string uploadFile,
			IEnumerable<string> parentIds, string descrp = "Uploaded with .NET!", string contentType = "application/json")
		{
			var body = new Google.Apis.Drive.v3.Data.File();
			body.Name = Path.GetFileName(uploadFile);
			body.Description = descrp;
			body.MimeType = contentType;
			body.Parents = parentIds?.ToList();

			using (var stream =	new FileStream(uploadFile, FileMode.Open, FileAccess.Read))
			{
				var request = service.Files.Create(body, stream, contentType);
				request.Fields = "files(id, name, parents, mimeType)";
				request.UseContentAsIndexableText = true;

				request.Upload();
				return request.ResponseBody;
			}
		}
	}
}
