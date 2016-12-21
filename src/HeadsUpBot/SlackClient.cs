using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using log4net;
using Newtonsoft.Json;

namespace HeadsUpBot
{
   // i pulled some this from github https://gist.github.com/jogleasonjr/7121367 and extended it
   //A simple C# class to post messages to a Slack channel
   //Note: This class uses the Newtonsoft Json.NET serializer available via NuGet
   class SlackClient
   {
      private readonly Uri _uri;
      private readonly Encoding _encoding = new UTF8Encoding();

      private ILog log = LogManager.GetLogger(typeof(Bot));


      public SlackClient(string urlWithAccessToken)
      {
         _uri = new Uri(urlWithAccessToken);
      }

      //Post a message using simple strings
      public void PostMessage(string text, string username = null, string channel = null)
      {
         Payload payload = new Payload()
         {
            Channel = channel,
            Username = username,
            Text = text
         };

         PostMessage(payload);
      }

      //Post a message using a Payload object
      public void PostMessage(Payload payload)
      {
         string payloadJson = JsonConvert.SerializeObject(payload);

         using (WebClient client = new WebClient())
         {
            NameValueCollection data = new NameValueCollection();
            data["payload"] = payloadJson;

            try
            {
               var response = client.UploadValues(_uri, "POST", data);
               //The response text is usually "ok"

               string responseText = _encoding.GetString(response);
            }
            catch (Exception ex)
            {
               log.Error(ex);
            }
         }
      }
   }

   //This class serializes into the Json payload required by Slack Incoming WebHooks
   public class Payload
   {
      [JsonProperty("channel")]
      public string Channel { get; set; }

      [JsonProperty("username")]
      public string Username { get; set; }

      [JsonProperty("text")]
      public string Text { get; set; }

      [JsonProperty("attachments")]
      public ICollection<SlackAttachment> Attachments { get; set; }

      public Payload()
      {
         this.Attachments = new List<SlackAttachment>();
      }

   }

   public class SlackAttachment
   {

      [JsonProperty("fallback")]
      public string Fallback { get; set; }

      [JsonProperty("color")]
      public string Color { get; set; }

      [JsonProperty("pretext")]
      public string Pretext { get; set; }

      [JsonProperty("author_name")]
      public string AuthorName { get; set; }

      [JsonProperty("author_link")]
      public string AuthorLink { get; set; }

      [JsonProperty("author_icon")]
      public string AuthorIcon { get; set; }

      [JsonProperty("title")]
      public string Title { get; set; }

      [JsonProperty("title_link")]
      public string TitleLink { get; set; }

      [JsonProperty("text")]
      public string Text { get; set; }

      [JsonProperty("fields")]
      public ICollection<Field> Fields { get; set; }

      [JsonProperty("image_url")]
      public string ImageUrl { get; set; }

      [JsonProperty("thumb_url")]
      public string ThumbUrl { get; set; }

      [JsonProperty("footer")]
      public string Footer { get; set; }

      [JsonProperty("footer_icon")]
      public string FooterIcon { get; set; }

      [JsonProperty("ts")]
      public int Ts { get; set; }

      [JsonProperty("mrkdwn_in")]
      public string[] MarkdownIn => new[] {"text"};

      public SlackAttachment()
      {
         this.Fields = new List<Field>();
      }

      public class Field
      {
         [JsonProperty("title")]
         public string Ttile { get; set; }

         [JsonProperty("value")]
         public string Value { get; set; }

         [JsonProperty("short")]
         public bool Short { get; set; }
      }
   }

}
