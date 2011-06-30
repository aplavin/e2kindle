using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using e2Kindle.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace e2Kindle
{
    public struct GoogleFeed
    {
        public string Id { get; set; }
        public string Url { get { return Id.Substring(5); } set { Id = "feed/" + value; } }
        public string Title { get; set; }
        public int UnreadCount { get; set; }
        public ImageSource Icon { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Title, Url);
        }
    }

    public class GoogleFeedEntry : IEquatable<GoogleFeedEntry>
    {
        public string Id;
        public string Link;
        public string Title;
        public string Content;
        public DateTime Published;
        public GoogleFeed Feed;

        public bool Equals(GoogleFeedEntry other)
        {
            return other != null && Id == other.Id;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Title, Link);
        }
    }

    public static class GoogleReader
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static bool _loggedIn;
        private static string _sid;
        private static string _auth;

        #region token
        private static string _token;
        private static DateTime _tokenTaken = DateTime.MinValue;
        private static readonly TimeSpan _tokenExpire = TimeSpan.FromMinutes(10);
        private static string Token
        {
            get
            {
                if (_token == null || DateTime.Now - _tokenTaken > _tokenExpire)
                {
                    _token = GetToken();
                    _tokenTaken = DateTime.Now;
                }
                return _token;
            }
        }

        private static string GetToken()
        {
            Login();
            return GetString("api/0/token");
        }
        #endregion

        /// <summary>
        /// Resets state of this class. Login is needed after it.
        /// </summary>
        public static void Reset()
        {
            logger.Debug("Reset");
            _loggedIn = false;
            _sid = null;
            _auth = null;
        }

        /// <summary>
        /// Gets unread entries from the specified feed.
        /// </summary>
        /// <param name="feed"></param>
        /// <returns>Empty enumerable if can't load or parse feed</returns>
        public static IEnumerable<GoogleFeedEntry> GetEntries(GoogleFeed feed)
        {
            return GetJson(string.Format(
                   "api/0/stream/contents/feed/{0}?xt=user/-/state/com.google/read&n=1000&ck={1}",
                   feed.Url, (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds)
                   )["items"].
                   Select(v => new GoogleFeedEntry
                   {
                       Id = v["id"].Value<string>(),
                       Link = v["alternate"].First["href"].Value<string>(),
                       Title = v["title"] != null ?
                       v["title"].Value<string>() :
                       "[без названия]",
                       Content =
                       v["summary"] != null && v["summary"]["content"] != null ?
                       v["summary"]["content"].Value<string>() :
                       "[пусто]",
                       Published = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(v["published"].Value<long>()),
                       Feed = feed
                   });
        }

        /// <summary>
        /// Gets unread entries for all specified feeds.
        /// </summary>
        /// <param name="feeds"></param>
        /// <returns></returns>
        public static IEnumerable<IGrouping<GoogleFeed, GoogleFeedEntry>> GetEntries(IEnumerable<GoogleFeed> feeds)
        {
            if (feeds == null) return null;
            if (feeds.Empty()) return Enumerable.Empty<IGrouping<GoogleFeed, GoogleFeedEntry>>();

            int degree = Math.Min(63, feeds.Count());
            return feeds.
                AsParallel().WithDegreeOfParallelism(degree).
                SelectMany(GetEntries).
                GroupBy(e => e.Feed).
                ToList().AsReadOnly();
        }

        /// <summary>
        /// Get subscribed feeds from google account.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<GoogleFeed> GetFeeds()
        {
            var unread = GetJson("api/0/unread-count?output=json")["unreadcounts"].
                Where(f => ((string)f["id"]).StartsWith("feed/")).
                ToDictionary(f => (string)f["id"], f => (int)f["count"]);

            var titles = GetJson("api/0/subscription/list?output=json")["subscriptions"].
                Where(f => ((string)f["id"]).StartsWith("feed/")).
                ToDictionary(f => (string)f["id"], f => (string)f["title"]);

            return titles.Select(p => new GoogleFeed
            {
                Url = p.Key.Substring(5),
                Title = p.Value,
                UnreadCount = unread.ContainsKey(p.Key) ? unread[p.Key] : 0,
                Icon = GetFavicon(p.Key.Substring(5))
            });
        }

        /// <summary>
        /// Marks all entries of the specified feed as read.
        /// </summary>
        /// <param name="feed"></param>
        public static void MarkAsRead(GoogleFeed feed)
        {
            string res = GetString("api/0/mark-all-as-read",
                new { s = feed.Id, t = feed.Title, T = Token });
            if (res != "OK")
            {
                throw new Exception("Returned result wasn't 'OK'.");
            }
        }

        /// <summary>
        /// Marks all entries of the specified feeds as read.
        /// </summary>
        /// <param name="feeds"></param>
        public static void MarkAsRead(IEnumerable<GoogleFeed> feeds)
        {
            if (feeds.Empty()) return;

            int degree = Math.Min(63, feeds.Count());
            feeds.AsParallel().WithDegreeOfParallelism(degree).ForAll(MarkAsRead);
        }

        /// <summary>
        /// Marks the specified entry as read.
        /// </summary>
        /// <param name="entry"></param>
        public static void MarkAsRead(GoogleFeedEntry entry)
        {
            string res = GetString("api/0/edit-tag", new
            {
                i = entry.Id,
                a = "user/-/state/com.google/read",
                ac = "edit",
                T = Token,
            });
        }

        /// <summary>
        /// Marks all specified entries as read.
        /// </summary>
        /// <param name="entries"></param>
        public static void MarkAsRead(IEnumerable<GoogleFeedEntry> entries)
        {
            if (entries.Empty()) return;

            int degree = Math.Min(63, entries.Count());
            entries.AsParallel().WithDegreeOfParallelism(degree).ForAll(MarkAsRead);
        }

        /// <summary>
        /// Logs into google account only in not logged in still (or Reset was called). Otherwise just returns.
        /// Username and password from settings are used.
        /// </summary>
        private static void Login()
        {
            if (_loggedIn) return;

            var loginRequest = (HttpWebRequest)WebRequest.Create(@"https://www.google.com/accounts/ClientLogin");

            byte[] requestContent = Encoding.UTF8.GetBytes(
                "service={service}&Email={user}&Passwd={pass}&continue=http://www.google.com/".
                    Format(new { service = "reader", user = Settings.Default.Username, pass = Settings.Default.Password })
                );

            loginRequest.Method = "POST";
            loginRequest.ContentType = "application/x-www-form-urlencoded";
            loginRequest.ContentLength = requestContent.Length;

            using (Stream requestStream = loginRequest.GetRequestStream())
            {
                // add form data to request stream
                requestStream.Write(requestContent, 0, requestContent.Length);
            }

            string data;
            using (var response = loginRequest.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var sr = new StreamReader(responseStream))
            {
                data = sr.ReadToEnd();
            }

            _sid = data.Substring((data.IndexOf("SID=") + 4), (data.IndexOf("\n") - 4)).Trim();
            _auth = data.Substring(data.IndexOf("Auth=") + 5).Trim();

            _loggedIn = true;
        }

        /// <summary>
        /// Get favicon for the host of the url. Icon is fetched from google cache in png format.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static BitmapImage GetFavicon(string url)
        {
            Uri srcUri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out srcUri))
            {
                logger.Error("Malformed url for favicon: {0}", url);
                return null;
            }
            url = string.Format("http://s2.googleusercontent.com/s2/favicons?domain={0}", srcUri.Host);

            BitmapImage image = new BitmapImage(new Uri(url));
            return image;
        }

        /// <summary>
        /// Gets response from address "http://www.google.com/reader/{url}" using SID and Auth from Login method.
        /// If not logged in - logs in.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static string GetString(string url, object data = null)
        {
            Login();

            bool isData = (data != null);
            byte[] bytes = null;
            if (isData)
            {
                var values = data.GetType().GetMembers().Where(
                    m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property).
                    ToDictionary(m => m.Name, m => ((PropertyInfo)m).GetValue(data, null));
                string post = values.
                    Aggregate("", (s, el) => s + string.Format("&{0}={1}", el.Key, el.Value)).
                    Substring(1);
                bytes = new ASCIIEncoding().GetBytes(post);
            }

            var request = (HttpWebRequest)WebRequest.Create(string.Format(@"http://www.google.com/reader/{0}", url));

            request.Headers.Add("Authorization", string.Format("GoogleLogin auth={0}", _auth));
            request.CookieContainer = new CookieContainer();
            request.CookieContainer.Add(new Cookie("SID", _sid, "/", ".google.com"));

            if (isData)
            {
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = bytes.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            else
            {
                request.Method = "GET";
            }

            using (var response = request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var reader = new StreamReader(responseStream))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Gets api result from google reader as JSON object.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static JObject GetJson(string url, object data = null)
        {
            return JObject.Parse(GetString(url, data));
        }
    }
}
