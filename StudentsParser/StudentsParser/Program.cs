using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Text.RegularExpressions;
using System.Net.Mail;

namespace StudentsParser
{
    class Program
    {

        private static string username;
        private static string password;
        private static int interval;
        private static string mailAddress;

        public static HttpClientHandler handler = new HttpClientHandler() {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip,
            UseCookies = true
        };
        public static HttpClient client = new HttpClient(handler);

        static void Main(string[] args)
        {
            username = args[0];
            password = args[1];
            interval = Convert.ToInt32(args[2]);
            mailAddress = args[3];
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,el;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Connection", "keep-alive");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            var timer = new System.Threading.Timer((e) => { RunParser(); }, null, TimeSpan.Zero, TimeSpan.FromMinutes(interval));
            Console.ReadKey();
        }

        static void RunParser()
        {
            Login().Wait();
            GetGrades().Wait();
            client.DefaultRequestHeaders.Remove("Cache-Control");
            client.DefaultRequestHeaders.Remove("Pragma");
            client.DefaultRequestHeaders.Remove("Origin");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
        }

        static async Task Login()
        {
            
            var res = await client.GetAsync("https://students.unipi.gr");
            var stringhtml = await res.Content.ReadAsStringAsync();
            string headerkey = (stringhtml.Remove(0, stringhtml.IndexOf("[2], '") + 6)).Replace("'", "").Replace("+", "").Remove(132);
            headerkey = UnescapeCodes(headerkey);
            //Console.WriteLine(headerkey);
            string headervalue = (stringhtml.Remove(0, stringhtml.IndexOf("[0], '") + 6)).Replace("'", "").Replace("+", "").Remove(768);
            headervalue = UnescapeCodes(headervalue);
            //Console.WriteLine(headervalue);
            if (headerkey.Length != 33 || headervalue.Length != 192)
            {
                await Login();
                return;
            }
            client.DefaultRequestHeaders.Remove("Cache-Control");
            client.DefaultRequestHeaders.Remove("Pragma");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "max-age=0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://students.unipi.gr/");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://students.unipi.gr");
            res = await client.PostAsync("https://students.unipi.gr/login.asp", new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("userName", username),
                new KeyValuePair<string, string>("pwd", password),
                new KeyValuePair<string, string>("submit1", "Είσοδος"),
                new KeyValuePair<string, string>("loginTrue", "login"),
                new KeyValuePair<string, string>(headerkey, headervalue)
            }));
            
        }

        static async Task GetGrades()
        {
            var res = await client.GetAsync("https://students.unipi.gr/stud_CResults.asp");
            var stringhtml = res.Content.ReadAsStringUTF8Async().Result;
            stringhtml = stringhtml.Remove(0, stringhtml.IndexOf("<?xml version=\"1.0\""));
            stringhtml = stringhtml.Remove(stringhtml.IndexOf("</td></tr></table></td></tr>")+28);
            stringhtml = Regex.Replace(stringhtml, "<[0-9A-Za-z\\?\\\"\\-\\.\\=\\s\\%\\/\\#]*>", " ");
            var gradesTxt = readGradesRegex(stringhtml);
            if (File.Exists("grades.txt"))
            {
                var fileIn = File.ReadAllLines("grades.txt");
                checkDiff(gradesTxt, fileIn);
            }
            var file = File.CreateText("grades.txt");
            file.WriteLine(gradesTxt);
            file.Close();
            file.Dispose();
        }

        public static string readGradesRegex(string src)
        {

            var rx = new Regex("(\\([Α-Ω0-9-]+\\))\\s+([Α-ΩA-Z\\s-]+)[0-9]\\s*[0-9]\\s*[0-9]\\s*([0-9-]+)\\s*(-*[Α-Ω\\s0-9-]*)\\s");

            var res = new StringBuilder();
            var resConsole = new StringBuilder();

            foreach (Match m in rx.Matches(src))
            {
                var subjCode = m.Groups[1].ToString().Trim();
                var subjName = Regex.Replace(m.Groups[2].ToString().Replace("ΥΠΟΧΡΕΩΤΙΚΟ", "").Replace("ΕΠΙΛΟΓΗΣ", "").Replace("ΞΕΝΗ ΓΛΩΣΣΑ", "").Trim(), "\\s+", " ");
                var subjGrade = m.Groups[3].ToString().Trim();
                var subjPeriod = Regex.Replace(Regex.Replace(m.Groups[4].ToString().Trim().Replace("\xA0", string.Empty), "\\t*(\\r\\n)*", ""), "\\s\\s\\s\\s", " ");
                resConsole.Append(subjCode + "\t" + subjName + "\t" + subjGrade + "\t" + subjPeriod + "\r\n");
                res.Append(subjCode + "\r\n" + subjName + "\r\n" + subjGrade + "\r\n" + subjPeriod + "\r\n");
            }
            Console.WriteLine(resConsole);
            return res.ToString();
        }

        public static void checkDiff(string res, string[] fileIn)
        {
            var counter = 0;
            var newGrades = Regex.Split(res, "\r\n|\r|\n");
            foreach (var line in newGrades)
            {
                if (counter != 0 && ((counter+1) % 4 != 0))
                {
                    counter++;
                    continue;
                }
                if (!line.Equals(fileIn[counter]))
                {
                    var msg = string.Format(@"{0}, {1}, {2}, {3}", newGrades[counter - 3], newGrades[counter - 2], newGrades[counter - 1], newGrades[counter]);
                    
                    MailMessage mail = new MailMessage();
                    SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");

                    mail.From = new MailAddress("yourmailaddress@gmail.com");
                    mail.To.Add(mailAddress);
                    mail.Subject = "ΝΕΟ ΜΑΘΗΜΑ";
                    mail.Body = msg;

                    SmtpServer.Port = 587;
                    SmtpServer.Credentials = new System.Net.NetworkCredential("yourmailaddress@gmail.com", "yourapppassword"); //follow the steps here https://support.google.com/mail/answer/185833?hl=en
                    SmtpServer.EnableSsl = true;
                    try
                    {
                        SmtpServer.Send(mail);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                counter++;
            }
        }

        public static string UnescapeCodes(string src)
        {
            var rx = new Regex("\\\\x([0-90-9]+)");
            var res = new StringBuilder();
            var pos = 0;
            foreach (Match m in rx.Matches(src))
            {
                res.Append(src.Substring(pos, m.Index - pos));
                pos = m.Index + m.Length;
                res.Append((char)Convert.ToInt32(m.Groups[1].ToString(), 16));
            }
            res.Append(src.Substring(pos));
            return res.ToString();
        }
    }

    public static class HttpContentExtension
    {
        public static async Task<string> ReadAsStringUTF8Async(this HttpContent content)
        {
            return await content.ReadAsStringAsync(Encoding.GetEncoding("ISO-8859-7"));
        }

        public static async Task<string> ReadAsStringAsync(this HttpContent content, Encoding encoding)
        {
            using (var reader = new StreamReader((await content.ReadAsStreamAsync()), encoding))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
