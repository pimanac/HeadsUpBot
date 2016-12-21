## HeadsUpBot

A slack helper bot for reddit moderators written in dotnet.

Supports Mono

#### Features

* Send alerts to slack when items have a high number of reports.
* Send alerts to slack when unmoderated items are on your front page
* Mirror Modmail, Comments and Submissions in realtime to dedicated slack channels.


#### Building

copy `App.config.example` to `App.config`

build the solution with visual studio or msbuild, your choice.

#### Configuration

Modify App.config to configure the application.

###### Reddit

Specify a reddit user and subreddit to watch.  OAuth is required.

    <add key="clientId" value="aaaaaaaaaaaaaa" />
    <add key="clientSercret" value="bbbbbbbbbbbbbbbbbbbbbbbbbbb" />
    <add key="redirectUri" value="http://127.0.0.1:00000" />
    <add key="user" value="foo" />
    <add key="password" value="bar" />
    <add key="subreddit" value="/r/example" />

###### Slack

configure the webhook

    <add key="slackuri" value="https://hooks.slack.com/services/xxxxxxxxx/yyyyyyyyy/zzzzzzzzzzzzzzzzzzzzzzzz" />

defalt bot username

    <add key="slackuser" value="Heads Up Bot" />

and channel to which alerts should be directed. 

    <add key="slackchannel" value="#general" />


###### General options

How long to sleep between checking the queues in minutes.  Be kind to the reddit api.

    <add key="sleepMinutes" value="15" />

Ping the channel when over limits below

    <add key="doHereAlerts" value="false" />

ping when there are items with this score

    <add key="overScoreThreshold" value="100" />

ping when there are this many unmoderated items in HOT

      <add key="unmodThreshold" value="15" />

ping when there are items with this many reports

      <add key="reportThreshold" value="5" />

###### Mirroring subreddit activity to slack

HeadsUpBot can mirror comment, submission, and modmail activity in your subreddit to one or more slack channels.
Items are yielded in near-realtime (depending on the activity in your subreddit).

**Warning** If your team is using a *Free* slack account your history is limited to 10,000 messages.
depending on the amount of activity in the target subreddit that can be quickly used up.

Comments

    <add key="commentStreamChannel" value="#commentstream" />

Submissions  Set `value=""` to disable Submission mirroring.

    <add key="submissionStreamChannel" value="#submissionstream" />

Modmail

    <add key="modmailStreamChannel" value="#modmail" />

