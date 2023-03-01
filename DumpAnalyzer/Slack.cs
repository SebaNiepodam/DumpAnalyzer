using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;

namespace DumpAnalyzer
{
    public static class Slack
    {
        public async static void SendMessage(string content, string title)
        {
            return; // sending not needed in this version :)
            try
            {
                var client = new HttpClient();

                //        curl - d "text=This is a block of text"
                //http://api.repustate.com/v2/demokey/score.json

                //curl - F content = "Hello" - F token = xxxx - xxxxxxxxx - xxxx https://slack.com/api/files.upload

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                // Create the HttpContent for the form to be posted.
                FormUrlEncodedContent requestContent = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("content", content),
                    new KeyValuePair<string, string>("token", ""),
                    new KeyValuePair<string, string>("title", title),
                    new KeyValuePair<string, string>("channels", Properties.Settings.Default.SLACK_Channel),
                    new KeyValuePair<string, string>("display_as_bot", "true"),
                    new KeyValuePair<string, string>("as_user", "false")
                });

                // Get the response.
                HttpResponseMessage response = await client.PostAsync(
                    "https://slack.com/api/files.upload",
                    requestContent);

                // Get the response content.
                HttpContent responseContent = response.Content;

                // Get the stream of the content.
                using (var reader = new StreamReader(await responseContent.ReadAsStreamAsync()))
                {
                    // Write the output.
                    Console.WriteLine(await reader.ReadToEndAsync());
                }
            }
            catch (System.Exception ex)
            {
                Logger.Log("Error on SendCodeSnippet: " + ex.ToString());
                MessageBox.Show("Error on SendCodeSnippet: " + ex.ToString());
            }
        }
    }
}
