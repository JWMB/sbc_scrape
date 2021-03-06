﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MediusFlowAPI.Models.Comment2
{
	// <auto-generated />
	//
	// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
	//
	//    using QuickType;
	//
	//    var response = Response.FromJson(jsonString);
		using System;
		using System.Collections.Generic;

		using System.Globalization;
	using System.Linq;
	using Newtonsoft.Json;
		using Newtonsoft.Json.Converters;

	public partial class Response
	{
		[JsonProperty("id")]
		public long Id { get; set; }

		[JsonProperty("tokenizedText")]
		public TokenizedText[] TokenizedText { get; set; }

		[JsonProperty("author")]
		public string Author { get; set; }

		[JsonProperty("createdDate")]
		public string CreatedDate { get; set; }

		[JsonProperty("isCarriedOver")]
		public bool IsCarriedOver { get; set; }

		[JsonProperty("hash")]
		public Guid Hash { get; set; }
	}

	public partial class TokenizedText
	{
		[JsonProperty("tokenType")]
		public string TokenType { get; set; }

		[JsonProperty("tokenValue")]
		public TokenValue TokenValue { get; set; }
	}

	public partial class TokenValue
	{
		[JsonProperty("value")]
		public string Value { get; set; }
	}

	public partial class Response
	{
		public static Response[] FromJson(string json) => JsonConvert.DeserializeObject<Response[]>(json, Converter.Settings);
	}

	public partial class Response
	{
		public Comment.Response ToCommentResponse()
		{
			return new Comment.Response
			{
				Type = "Medius.Core.Services.Comment, Medius.Core.Common",
				Author = Author,
				CreatedDate = CreatedDate,
				Id = Id,
				Text = string.Join("\n", TokenizedText.Select(t => t.TokenValue?.Value))
			};
		}
	}
	public static class Serialize
	{
		public static string ToJson(this Response[] self) => JsonConvert.SerializeObject(self, Converter.Settings);
	}

	internal static class Converter
	{
		public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
		{
			MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
			DateParseHandling = DateParseHandling.None,
			Converters =
			{
				new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
			},
		};
	}
}
