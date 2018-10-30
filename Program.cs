using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace uglydirtysendmail
{
   class Program
   {
      static void Main(string[] args)
      {
         var argsline = string.Join(' ', args);
         Console.WriteLine(argsline);
         var hostMatch = Regex.Match(argsline, @"-h (?<hostname>[^\s\:]+)(?:\:(?<port>\d+))?").Groups;
         var host = hostMatch["hostname"].Value;
         int port = 0;
         if (!int.TryParse(hostMatch["port"]?.Value, out port) || port == 0)
            port = 25;
         Console.WriteLine($"Host: {host}");
         Console.WriteLine($"Port: {port}");

         var user = Regex.Match(argsline, @"-u (?<user>[^\s]+)").Groups["user"].Value;
         Console.WriteLine($"User: {user}");
         var pass = Regex.Match(argsline, @"-p (?<pass>[^\s]+)").Groups["pass"].Value;
         Console.WriteLine($"Pass: {pass}");
         var from = Regex.Match(argsline, @"-f (?<from>[^\s]+)").Groups["from"].Value;
         bool sslEnabled = false;
         Console.WriteLine($"SSL Enabled: {sslEnabled}");
         Console.WriteLine($"from: {from}");
         var to = Regex.Match(argsline, @"-to (?<to>[^\s]+)").Groups["to"].Value;
         Console.WriteLine($"to: {to}");
         string subject = "";
         string body = "";
         if (Console.IsInputRedirected)
         {
            (subject, body) = ReadRedirectedInput();
         }

         Console.WriteLine($"Subject: {subject}");
         Console.WriteLine("Sending...");
         var client = new SmtpClient(host, port);
         client.EnableSsl = sslEnabled;
         client.UseDefaultCredentials = false;
         client.Credentials = new System.Net.NetworkCredential(user, pass);
         var msg = new MailMessage(from, to);
         msg.Subject = subject;
         //msg.IsBodyHtml = true;
         var msHtml = new MemoryStream(Encoding.UTF8.GetBytes(body));
            var html = new AlternateView(msHtml, System.Net.Mime.MediaTypeNames.Text.Html);
            msg.AlternateViews.Add(html);


         var msText = new MemoryStream(Encoding.UTF8.GetBytes("VERSIONE SOLO TESTO"));
            var text = new AlternateView(msText, System.Net.Mime.MediaTypeNames.Text.Plain);
            msg.AlternateViews.Add(text);

         client.Send(msg);
         Console.WriteLine("Message sent.");
      }

      private static (string subject, string body) ReadRedirectedInput()
      {
         string stdin = null;
         using (Stream stream = Console.OpenStandardInput())
         {
            byte[] buffer = new byte[1000]; // Use whatever size you want
            StringBuilder builder = new StringBuilder();
            int read = -1;
            while (true)
            {
               AutoResetEvent gotInput = new AutoResetEvent(false);
               Thread inputThread = new Thread(() =>
               {
                  try
                  {
                     read = stream.Read(buffer, 0, buffer.Length);
                     gotInput.Set();
                  }
                  catch (ThreadAbortException)
                  {
                     Thread.ResetAbort();
                  }
               })
               {
                  IsBackground = true
               };

               inputThread.Start();

               // Timeout expired?
               if (!gotInput.WaitOne(100))
               {
                  inputThread.Abort();
                  break;
               }

               // End of stream?
               if (read == 0)
               {
                  stdin = builder.ToString();
                  break;
               }

               // Got data
               builder.Append(Console.InputEncoding.GetString(buffer, 0, read));
            }
         }
         StringBuilder bodyBuilder = new StringBuilder();
         string subject = "";
         using (StringReader reader = new StringReader(stdin))
         {
            var line = reader.ReadLine();
            subject = Regex.Match(line, @"^Subject:(?<subject>.+)").Groups["subject"].Value?.Trim();
            if (subject.Length == 0)
               bodyBuilder.AppendLine(line);
            bodyBuilder.Append(reader.ReadToEnd());
         }
         return (subject, bodyBuilder.ToString());
      }
   }
}