using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using RedditSharp;
using RedditSharp.Things;
using static System.Configuration.ConfigurationManager;

namespace HeadsUpBot
{
   class Bot
   {
      private Subreddit subreddit;
      private Reddit reddit;
      private BotWebAgent agent;
      private SlackClient slack;
      private Random random;

      private bool doHereAlerts = true;
      private string slackChannel;
      private string slackUser;
      private string subredditName;
      private int overScoreThreshold = 750;
      private int unmodThreshold = 15;
      private int reportThreshold = 5;

      private IList<string> doneModmails;
      private IList<PrivateMessage> modmailCache;
      private IList<string> moderators;

      private Dictionary<string, string> userColors;

      private ILog log = LogManager.GetLogger(typeof(Bot));

      public Bot()
      {
         this.overScoreThreshold = Int32.Parse(AppSettings["overScoreThreshold"]);
         this.unmodThreshold = Int32.Parse(AppSettings["unmodThreshold"]);
         this.doHereAlerts = Boolean.Parse(AppSettings["doHereAlerts"]);
         this.moderators = new List<string>();
         this.doneModmails = new List<string>();
         this.subredditName = AppSettings["subreddit"]
            .StartsWith("/r/")
            ? AppSettings["subreddit"]
            : "/r" + AppSettings["subreddit"];
         this.slackUser = String.IsNullOrEmpty(AppSettings["slackuser"]) ? "Heads Up Bot" : AppSettings["slackuser"];
         this.random = new Random();
         this.userColors = new Dictionary<string, string>();
      }

      public void LogIn()
      {
         slackChannel = AppSettings["slackchannel"];

         agent = new BotWebAgent(AppSettings["user"],
            AppSettings["password"],
            AppSettings["clientId"],
            AppSettings["clientSecret"],
            AppSettings["redirectUri"]
         );

         reddit = new Reddit(agent, true);
         reddit.RateLimit = WebAgent.RateLimitMode.Pace;
         subreddit = reddit.GetSubreddit(subredditName);

         foreach (var mod in subreddit.Moderators)
            moderators.Add(mod.Name);

         SlackLogin();
      }

      public void ModmailStream()
      {
         var startedUtc = DateTime.UtcNow;
         var stream = subreddit.Modmail.GetListingStream(100).Where(x => x.SentUTC >= startedUtc.AddHours(-5)).Take(4);
         foreach (var item in stream)
         {

            var payload = new Payload();
            payload.Username = "Modmail received - " + item.Id;

            var dest = item.Destination;
            if (dest == subredditName)
               dest = "";
            else
               dest = "   |   _" + dest + "_";

            payload.Text = "*Subject:* " + "<https://www.reddit.com/message/messages/" + item.Id + "|" + item.Subject +
                           ">" + dest;
            payload.Channel = AppSettings["modmailStreamChannel"].ToString();

            if (!String.IsNullOrEmpty(item.ParentID))
            {
               payload.Attachments.Add(new SlackAttachment
               {
                  Title = "",
                  Text = item.Parent.Body,
                  AuthorName = item.Parent.Author,
                  AuthorLink = "https://www.reddit.com/user/" + item.Parent.Author,
                  Ts = DateTimeToUnixEpoch(item.Parent.SentUTC),
                  Color = "danger"
               });
            }
            payload.Attachments.Add(new SlackAttachment
            {
               Title = "",
               Text = item.Body,
               AuthorName = item.Author,
               AuthorLink = "https://www.reddit.com/user/" + item.Author,
               Footer = "<https://www.reddit.com/message/messages/" + item.Id + "|======click here to reply======>",
               Ts = DateTimeToUnixEpoch(item.SentUTC),
               Color = "good"
            });

            slack.PostMessage(payload);
            System.Threading.Thread.Sleep(1000);
         }
      }

      public void CommentStream()
      {
         var lastChunk = DateTime.Now;
         var chunk = new List<Comment>();
         var startedUtc = DateTime.UtcNow;
         var stream = subreddit.CommentStream.Where(x => x.CreatedUTC >= startedUtc);

         int i = 0;
         foreach (var comment in stream)
         {
            chunk.Add(comment);
            if (chunk.Count >= 10 || DateTime.Now.Subtract(lastChunk).Seconds >= 2)
            {
               SendChunk(chunk);
               lastChunk = DateTime.Now;
               chunk.Clear();
            }
         }
      }

      public void SubmissionStream()
      {
         var startedUtc = DateTime.UtcNow;
         foreach (var item in subreddit.SubmissionStream.Where(x => x.CreatedUTC >= startedUtc))
         {
            var payload = new Payload();

            payload.Username = "New Post - " + item.Id;
            payload.Channel = AppSettings["submissionStreamChannel"].ToString();
            payload.Text = "<" + item.Url.AbsoluteUri + "|" + item.Title + ">";

            payload.Attachments.Add(new SlackAttachment
            {
               Title = "<" + item.Shortlink + "|Reddit post>",
               Text = item.IsSelfPost ? item.SelfText : "",
               AuthorName = item.AuthorName,
               AuthorLink = "https://www.reddit.com/user/" + item.AuthorName,
               Ts = DateTimeToUnixEpoch(item.CreatedUTC)
            });

            slack.PostMessage(payload);
            System.Threading.Thread.Sleep(1000);
         }
      }

      public void ProcessQueue()
      {
         var unmoderated = subreddit.Hot.Take(50).Where(x => String.IsNullOrEmpty(x.ApprovedBy)).ToList();

         if (unmoderated.Count == 0)
         {
            log.Info("Nothing to report - good job!");
            return;
         }

         var overScore = unmoderated.Where(x => x.Score > overScoreThreshold).ToList();
         var message = new StringBuilder();

         if (doHereAlerts && (unmoderated.Count > unmodThreshold || overScore.Count > 0))
            message.AppendLine($"<!here|here>");

         message.AppendLine("`Score  | R  | Author`");
         foreach (var item in unmoderated.OrderByDescending(x => x.Score))
         {
            var link = $"<{item.Shortlink}|{item.Title.PadRight(36).Substring(0, 36)} ...>";
            var reports = item.ModReports.Concat(item.UserReports).Count();

            message.AppendLine(
               $"`{item.Score.ToString().PadRight(6)} | {reports.ToString().PadRight(2)} | {item.AuthorName.PadRight(20)} |` {link}");
         }

         var payload = new Payload();
         payload.Username = slackUser;
         payload.Channel = slackChannel;
         payload.Text = "Unmoderated items in HOT";
         payload.Attachments.Add(new SlackAttachment
         {
            Color = "#2a3556",
            Text = message.ToString()
         });

         slack.PostMessage(payload);
      }

      public void ProcessReports()
      {
         IList<VotableThing> all = null;
         try
         {
            all = subreddit.ModQueue.GetListing(1000, 1000).ToList();
         }
         catch (Exception ex)
         {
            log.Error(ex);
         }

         if (all == null)
            return;

         if (all.Count == 0)
         {
            log.Info("Nothing to report - good job!");
            return;
         }

         var links = all.Where(x => x.Kind == "t3" && x.Distinguished == VotableThing.DistinguishType.None).ToList();
         var reportLinks = (from Post thing in links
                            select new
                               {
                                  Author = thing.AuthorName,
                                  Score = thing.Score,
                                  Text = thing.Title,
                                  ShortLink = thing.Shortlink,
                                  Reports = thing.ModReports.Concat(thing.UserReports).Sum(x => x.Count)
                               })
                               .Where(x => x.Reports >= reportThreshold)
                               .OrderByDescending(x => x.Reports)
                               .ToList();

         var comments = all.Where(x => x.Kind == "t1" && x.Distinguished == VotableThing.DistinguishType.None).ToList();

         var reportComments = (from Comment thing in comments
                               select new
                               {
                                  Author = thing.Author,
                                  Score = thing.Score,
                                  // when the comment starts with a quote it forces a newline in slack.
                                  Text = thing.Body.Replace(">","").Replace("&gt;",""),
                                  ShortLink = thing.Shortlink,
                                  Reports = thing.ModReports.Concat(thing.UserReports).Sum(x => x.Count)
                               })
                               .Where(x => x.Reports >= reportThreshold)
                               .OrderByDescending(x => x.Reports)
                               .ToList();


         if (reportLinks.Count() == 0 && reportComments.Count() == 0)
            return;

         var pmessage = new StringBuilder();
         pmessage.AppendLine("`Score  | R  | Author`");
         foreach (var item in reportLinks)
         {
            var link = item.ShortLink.Replace("https://oauth.reddit.com", "https://www.reddit.com");
            link = $"<{link}|{item.Text.PadRight(26).Substring(0, 26)} ...>";

            pmessage.AppendLine(
               $"`{item.Score.ToString().PadRight(6)} | {item.Reports.ToString().PadRight(2)} | {item.Author.PadRight(20)} |` {link}");
         }

         var cmessage = new StringBuilder();
         cmessage.AppendLine("`Score  | R  | Author`");
         foreach (var item in reportComments)
         {
            var link = item.ShortLink.Replace("https://oauth.reddit.com", "https://www.reddit.com");
            link = $"<{link}|{item.Text.PadRight(26).Substring(0, 26)} ...>";
            cmessage.AppendLine(
               $"`{item.Score.ToString().PadRight(6)} | {item.Reports.ToString().PadRight(2)} | {item.Author.PadRight(20)} |` {link}");
         }

         var payload = new Payload();
         payload.Username = slackUser;
         payload.Channel = slackChannel;
         payload.Text = "Items with a high number of reports.";

         if (reportLinks.Count() > 0)
         {
            payload.Attachments.Add(new SlackAttachment
            {
               Title = "Posts",
               TitleLink = "https://www.reddit.com" + subreddit.Url + "/about/modqueue?only=links",
               Color = "#2a3556",
               Text = pmessage.ToString()
            });
         }

         if (reportComments.Count() > 0)
         {
            payload.Attachments.Add(new SlackAttachment
            {
               Title = "Comments",
               TitleLink = "https://www.reddit.com" + subreddit.Url + "/about/modqueue?only=comments",
               Color = "#562a35",
               Text = cmessage.ToString()
            });
         }
         slack.PostMessage(payload);
      }

      public void ProcessModmail()
      {
         modmailCache = reddit.User.ModMail.Take(400)
                                          .Where(x => !doneModmails.Contains(x.Id))
                                          .ToList();

         if (modmailCache.Count == 0)
         {
            log.Info("no new modmails to process.");
            return;
         }

         // System.Threading.Thread.Sleep(5000);
         SearchModmail(new Regex(@"\b[Aa]\.?[Mm]\.?[Aa]\.?\b", RegexOptions.IgnoreCase), "AMA", "#ama");

         foreach (var modmail in modmailCache)
         {
            doneModmails.Add(modmail.Id);
         }

         modmailCache = null;
      }

      private void SlackLogin()
      {
         var slackuri = AppSettings["slackuri"];
         slack = new SlackClient(slackuri);
      }

      private void SendChunk(ICollection<Comment> chunk)
      {
         var payload = new Payload();

         payload.Username = "Heads up bot - messages (" + chunk.Count + ")";
         payload.Channel = AppSettings["commentStreamChannel"].ToString();

         foreach (var item in chunk)
         {
            payload.Attachments.Add(new SlackAttachment
            {
               Title = item.LinkTitle,
               TitleLink = item.Shortlink.Replace("https://oauth.reddit.com", "https://www.reddit.com"),
               Text = item.Body,
               AuthorName = item.Author,
               AuthorLink = "https://www.reddit.com/user/" + item.Author,
               Ts = DateTimeToUnixEpoch(item.CreatedUTC),
               Color = GetUserColor(item.Author)
            });
         }

         slack.PostMessage(payload);
      }

      private void SearchModmail(Regex regex, string rulename, string slackChannel)
      {
         var amaMail = modmailCache.Where(x => regex.IsMatch(x.Body) || regex.IsMatch(x.Subject)).ToList();

         if (amaMail.Count == 0)
            return;

         var message = new System.Text.StringBuilder();

         message.AppendLine($"There are {amaMail.Count} matching search: {rulename}.");
         message.AppendLine($"```{Environment.NewLine}Criteria:{Environment.NewLine}{regex.ToString()}{Environment.NewLine}```");
         message.AppendLine($"______________________________________________");

         foreach (PrivateMessage item in amaMail)
         {
            doneModmails.Add(item.Id);
            message.AppendLine($"<https://www.reddit.com/message/messages/{item.Id}|Message>  |  Author: {item.Author} - Unread: {item.Unread}");
         }

         if (!String.IsNullOrEmpty(message.ToString()))
         {
            slack.PostMessage(username: $"Heads Up Bot - mm search: {rulename}",
                              text: message.ToString(),
                              channel: slackChannel);
         }
      }

      private int DateTimeToUnixEpoch(DateTime time)
      {
         return (Int32)time.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
      }

      private string GetUserColor(string user)
      {
         if (!userColors.ContainsKey(user))
         {
            var color = "";
            color = String.Format("#{0:X6}", random.Next(0xffffff));
            userColors.Add(user, color);
         }
         return userColors[user];
      }
   }
}
