using System;
using System.ComponentModel;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using log4net;

using static System.Configuration.ConfigurationManager;

namespace HeadsUpBot
{
   class Program
   {
      private static Bot bot;
      private static int sleepMinutes = -1;
      private static ILog log = LogManager.GetLogger(typeof(Program));

      static void Main(string[] args)
      {
         try
         {
            var configs = new string[] { "subreddit","overScoreThreshold","unmodThreshold","sleepMinutes",
               "clientId", "clientSecret","user", "password", "slackchannel", "slackUri" };

            sleepMinutes = Int32.Parse(AppSettings["sleepMinutes"]);

            for (int i = 0; i <= configs.GetUpperBound(0); i++)
               if (String.IsNullOrEmpty(AppSettings[configs[i]]))
                  throw new ConfigurationErrorsException("invalid config option: " + configs[i]);
         }
         catch (Exception ex)
         {
            log.Error("invalid config.  press any key to exit.", ex);
            Console.Read();
            return;
         }

         bot = new Bot();
         log.Info("Logging into reddit");
         bot.LogIn();

         StartStreams();
         while (true)
         {
            log.Info("Doing Work");
            DoWork();
            log.Info("Sleeping...");
            Task.Delay(new TimeSpan(0, 0, sleepMinutes, 0)).Wait();
         }
      }

      private static void DoWork()
      {
         log.Info("Processing Queues");
         bot.ProcessQueue();

         log.Info("Processing AllReports");
         bot.ProcessReports();
      }

      private static void StartStreams()
      {
         if (AppSettings["commentStreamChannel"] != "")
         {
            log.Info("Starting comment stream");
            Task.Run(() => bot.CommentStream());
         }

         if (AppSettings["submissionStreamChannel"] != "")
         {
            log.Info("Starting Submission stream");
            Task.Run(() => bot.SubmissionStream());
         }

         if (AppSettings["modmailStreamChannel"] != "")
         {
            log.Info("Starting modmail stream");
            Task.Run(() => bot.ModmailStream());
         }
      }
   }
}
