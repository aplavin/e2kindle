using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using e2Kindle.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace e2Kindle
{
    [Serializable]
    public class GoogleReaderException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:e2Kindle.GoogleReaderException"/> class.
        /// </summary>
        public GoogleReaderException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:e2Kindle.GoogleReaderException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error. </param>
        public GoogleReaderException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:e2Kindle.GoogleReaderException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception. </param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified. </param>
        public GoogleReaderException(string message, Exception innerException) : base(message, innerException) { }
    }

    public struct GoogleFeed
    {
        public string Id { get; set; }
        public string Url
        {
            get
            {
                try
                {
                    return Id.Substring(5);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new GoogleReaderException(string.Format("Wrong feed Id '{0}'.", Id), ex);
                }
            }
        }
        public string Title { get; set; }
        public int UnreadCount { get; set; }
        public ImageSource Icon { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Title, Url);
        }
    }

    // Note: it should be a struct...
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
            return Id == other.Id;
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
        public static IEnumerable<GoogleFeedEntry> GetEntries(this GoogleFeed feed)
        {
            var json = GetJson(string.Format(
                   "api/0/stream/contents/feed/{0}?xt=user/-/state/com.google/read&n=1000&ck={1}",
                   feed.Url, (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds)
                   );
            try
            {
                return json["items"].
                         Select(v => new GoogleFeedEntry
                         {
                             Id = v["id"].Value<string>(),
                             Link = Utils.CatchNullReference(() => v["alternate"].First["href"].Value<string>(), null),
                             Title = Utils.CatchNullReference(() => v["title"].Value<string>(), "[No title]"),
                             Content = Utils.CatchNullReference(() => v["summary"]["content"].Value<string>(), null),
                             Published = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(v["published"].Value<long>()),
                             Feed = feed
                         });
            }
            catch (Exception ex)
            {
                throw new GoogleReaderException(
                    string.Format("JSON object with feed '{0}' entries hasn't all expected fields. Probably Google Reader API has changed.", feed.Id),
                    ex);
            }
        }

        /// <summary>
        /// Gets unread entries for all specified feeds.
        /// </summary>
        /// <param name="feeds"></param>
        /// <returns></returns>
        public static IEnumerable<IGrouping<GoogleFeed, GoogleFeedEntry>> GetEntries(this IEnumerable<GoogleFeed> feeds)
        {
            if (feeds == null) throw new ArgumentNullException("feeds");
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
            try
            {
                var unread = GetJson("api/0/unread-count?output=json")["unreadcounts"].
                    Where(f => ((string)f["id"]).StartsWith("feed/")).
                    ToDictionary(f => (string)f["id"], f => (int)f["count"]);

                var titles = GetJson("api/0/subscription/list?output=json")["subscriptions"].
                    Where(f => ((string)f["id"]).StartsWith("feed/")).
                    ToDictionary(f => (string)f["id"], f => (string)f["title"]);

                return titles.Select(p => new GoogleFeed
                {
                    Id = p.Key,
                    Title = p.Value,
                    UnreadCount = unread.ContainsKey(p.Key) ? unread[p.Key] : 0,
                    Icon = GetFavicon(p.Key.Substring(5))
                });
            }
            catch (SystemException sysEx) // don't catch GoogleReaderException
            {
                throw new GoogleReaderException("Getting feeds failed: probably Google Reader API has changed.", sysEx);
            }
        }

        /// <summary>
        /// Marks all entries of the specified feed as read.
        /// </summary>
        /// <param name="feed"></param>
        public static void MarkAsRead(this GoogleFeed feed)
        {
            string res = GetString("api/0/mark-all-as-read",
                new { s = feed.Id, t = feed.Title, T = Token });

            if (res != "OK")
            {
                throw new GoogleReaderException(string.Format("Mark feed '{0}' as read probably failed: Google didn't return OK.", feed.Id));
            }
        }

        /// <summary>
        /// Marks all entries of the specified feeds as read.
        /// </summary>
        /// <param name="feeds"></param>
        public static void MarkAsRead(this IEnumerable<GoogleFeed> feeds)
        {
            if (feeds == null) throw new ArgumentNullException("feeds");
            if (feeds.Empty()) return;

            int degree = Math.Min(63, feeds.Count());
            feeds.AsParallel().WithDegreeOfParallelism(degree).ForAll(MarkAsRead);
        }

        /// <summary>
        /// Marks the specified entry as read.
        /// </summary>
        /// <param name="entry"></param>
        public static void MarkAsRead(this GoogleFeedEntry entry)
        {
            string res = GetString("api/0/edit-tag", new
            {
                i = entry.Id,
                a = "user/-/state/com.google/read",
                ac = "edit",
                T = Token,
            });

            if (res != "OK")
            {
                throw new GoogleReaderException(string.Format("Mark entry '{0}' as read probably failed: Google didn't return OK.", entry.Id));
            }
        }

        /// <summary>
        /// Marks all specified entries as read.
        /// </summary>
        /// <param name="entries"></param>
        public static void MarkAsRead(this IEnumerable<GoogleFeedEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException("entries");
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

            try
            {
                var loginRequest = (HttpWebRequest)WebRequest.Create(@"https://www.google.com/accounts/ClientLogin");

                byte[] requestContent = Encoding.UTF8.GetBytes(
                    "service={service}&Email={user}&Passwd={pass}&continue=http://www.google.com/".
                        Format(new { service = "reader", user = Settings.Default.GoogleUser, pass = Settings.Default.GooglePassword })
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

                try
                {
                    _sid = data.Substring((data.IndexOf("SID=") + 4), (data.IndexOf("\n") - 4)).Trim();
                    _auth = data.Substring(data.IndexOf("Auth=") + 5).Trim();

                    _loggedIn = true;
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new GoogleReaderException("Wrong response format from Google ClientLogin, can't parse SID and Auth.", ex);
                }

            }
            catch (WebException webEx)
            {
                if (((HttpWebResponse)webEx.Response).StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new GoogleReaderException("Login to Google Reader failed: incorrect username or password.", webEx);
                }
                else
                {
                    throw new GoogleReaderException("Login to Google Reader failed: there are problems with your Internet connection or Google has changed its API.", webEx);
                }
            }
        }

        /// <summary>
        /// Get favicon for the host of the url. Icon is fetched from google cache in png format.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static BitmapImage GetFavicon(string url)
        {
            if (url == null) throw new ArgumentNullException("url");

            Uri srcUri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out srcUri))
            {
                throw new ArgumentException("URL specified to get favicon is malformed");
            }
            url = string.Format("http://s2.googleusercontent.com/s2/favicons?domain={0}", srcUri.Host);

            var image = new BitmapImage(new Uri(url));
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
            if (url == null) throw new ArgumentNullException("url");

            Login();

            bool hasData = (data != null);
            byte[] bytes = null;
            if (hasData)
            {
                var values = data.GetType().GetMembers().
                    Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property).
                    ToDictionary(m => m.Name, m => ((PropertyInfo)m).GetValue(data, null));
                string post = values.
                    Aggregate("", (s, el) => s + string.Format("&{0}={1}", el.Key, el.Value)).
                    Substring(1); // take substring to cut off the first '&' sign
                bytes = new ASCIIEncoding().GetBytes(post);
            }

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(string.Format(@"http://www.google.com/reader/{0}", url));

                request.Headers.Add("Authorization", string.Format("GoogleLogin auth={0}", _auth));
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.Add(new Cookie("SID", _sid, "/", ".google.com"));

                if (hasData)
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
            catch (WebException webEx)
            {
                throw new GoogleReaderException(
                    string.Format(
                    "Request to url '{0}' {1}at Google Reader failed: there are problems with your Internet connection or Google has changed its API.",
                    url,
                    hasData ? "(with additional POST data) " : ""),
                    webEx);
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
            if (url == null) throw new ArgumentNullException("url");

            try
            {
                return JObject.Parse(GetString(url, data));
            }
            catch (JsonReaderException jsonEx)
            {
                throw new GoogleReaderException(
                    string.Format("Request from URL '{0}' {1}wasn't in JSON format. Probably Google Reader API has changed.",
                    url,
                    data != null ? "(with additional POST data) " : ""),
                    jsonEx);
            }
            catch (Exception ex)
            {
                // Used JSON library can throw also just System.Exception
                if (ex.Message.ContainsCI("json"))
                {
                    throw new GoogleReaderException(
                        string.Format("Request from URL '{0}' {1}wasn't in JSON format. Probably Google Reader API has changed.",
                        url,
                        data != null ? "(with additional POST data) " : ""),
                        ex);
                }
                throw;
            }
        }
    }
}
