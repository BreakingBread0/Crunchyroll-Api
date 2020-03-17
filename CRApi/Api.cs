using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRApi {
    public static class Extensions {
        public static bool IsNullOrEmpty(this string input) => string.IsNullOrEmpty(input);
    }

    public class Crunchyroll {
        string BuildPOSTRequest(Dictionary<string, string> POSTData) {
            //?data=crunchy&data2=roll
            if (!string.IsNullOrWhiteSpace(_session_id))
                POSTData.Add("session_id", _session_id);
            POSTData.Add("locale", _locale);
            POSTData.Add("connectivity_type", "ethernet");
            POSTData.Add("version", "1.3.1.0");
            List<string> temp = new List<string>();
            foreach (KeyValuePair<string, string> data in POSTData)
                temp.Add(URLEncodeString(data.Key) + "=" + URLEncodeString(data.Value));
            return string.Join("&", temp.ToArray<string>());
        }

        private CookieContainer container = new CookieContainer();
        public string POST(string ApiMethod, string PostData, string proxy = null) {
            string method = $"https://api.crunchyroll.com/{ApiMethod}.0.json";
            byte[] postBytes = Encoding.Default.GetBytes(PostData);
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(method);

            if (!proxy.IsNullOrEmpty()) {
                var ValidIpAddressPortRegex = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5]):[\d]+$";
                if (!Regex.IsMatch(proxy, ValidIpAddressPortRegex)) {
                    return string.Empty;
                }
                request.Proxy = new WebProxy(proxy);
            }
            request.CookieContainer = container;
            request.ContentLength = postBytes.Length;

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            //request.UserAgent = "Dalvik/1.6.0 (Linux; U; Android 4.4.4; Samsung Build/KTU84P)";
            //AddHeaders(request.Headers);


            Stream postStream = request.GetRequestStream();
            postStream.Write(postBytes, 0, postBytes.Length);
            postBytes = null;
            postStream.Dispose();

            HttpWebResponse response = null;
            try {
                response = (HttpWebResponse)request.GetResponse();
                Stream Answer = response.GetResponseStream();
                StreamReader answer = new StreamReader(Answer);

                //if ((int)response.StatusCode != 200)
                //    Warning("Status code is " + response.StatusCode + " (" + (int)response.StatusCode + ")");

                string result = answer.ReadToEnd();
                //WriteLine(result);
                response.Close();

                return result;
            } catch (Exception e) {
                return string.Empty;
            } finally {
                response?.Dispose();
            }
            return string.Empty;
        }

        public string _session_id { get; private set; }
        public void SetSession(string session, string locale = "enUS") {
            _locale = locale;
            _session_id = session;
        }
        public string _locale;

        [Serializable]
        public class CRUnblock {
            public bool error;
            public string code;
            public data Data;
            public class data {
                public string session_id;
            }
        }

        public bool SpoofSessionWithUnblocker() {
            try {
                WebClient w = new WebClient();
                CRUnblock un = JsonConvert.DeserializeObject<CRUnblock>(w.DownloadString("https://api2.cr-unblocker.com/start_session"));
                if (un.error) {
                    throw new Exception(un.code);
                }
                SetSession(un.Data.session_id);
                return true;
            } catch {
#if DEBUG
                throw;
#endif
            }
            return false;
        }

        public bool SpoofSession(string proxy) {
            try {
                _locale = "enUS";
                string POSTData = BuildPOSTRequest(new Dictionary<string, string>() {
                        { "device_id", "00000000-4d19-2528-ffff-ffff99d603a9" }, //HWID?
                        { "device_type", "com.crunchyroll.windows.desktop" }, //App Name
                        { "access_token", "LNDJgOit5yaRIWN" } //Access token is found in the applications themselves!
                        //{ "locale", _locale } wird schon hinzugefügt
                    });

                string result = null;
                try {
                    result = POST("start_session", POSTData, proxy);
                } catch { }

                if (result == null || string.IsNullOrWhiteSpace(result)) {
                    return false;
                }
                JObject obj = JObject.Parse(result);
                if ((bool)obj["error"] == false) {
                    _session_id = (string)obj["data"]["session_id"];
                } else {
                    //Logger.Log(result);
                    throw new Exception($"{obj["code"]}: {obj["message"]}");
                }
                return true;
            } catch (Exception ex) {
#if DEBUG
                throw;
#endif
                //Logger.Log(ex.ToString());
                //Console.WriteLine(ex.ToString());
            }
            return false;
        }

        private static string URLEncodeString(string data) {
            return Flurl.Url.Encode(data);
        }

        [Serializable]
        public class Season {
            [JsonProperty(PropertyName = "collection_id")]
            public string season_id;
            public string name;
            [JsonProperty(PropertyName = "media_type")]
            public string type;
            //public string Name;
            [JsonProperty(PropertyName = "season")]
            public int season_number;
            public bool complete;
        }

        [Serializable]
        public class SearchResult {
            public string series_id;
            public string name;
            [JsonIgnore]
            public string portraitURL {
                get {
                    if (portrait_image == null)
                        return string.Empty;
                    return portrait_image["full_url"].ToObject<string>();
                }
            }
            [JsonProperty(PropertyName = "portrait_image", Required = Required.Always)]
            private JObject portrait_image;
            public bool premium_only;
        }

        [Serializable]
        public class Episode {
            [JsonProperty(PropertyName = "media_id")]
            public string episode_id;
            public int episode_number;
            public bool premium_only;
            public string name;
            public int duration;
            [JsonIgnore]
            public string image {
                get {
                    //if (screenshot_image == null)
                    //return string.Empty;
                    return screenshot_image["full_url"].ToObject<string>();
                }
            }
            [JsonProperty(PropertyName = "screenshot_image", Required = Required.Always)]
            private JObject screenshot_image;
        }

        [Serializable]
        public class StreamInfo {
            [JsonProperty(PropertyName = "free_available", Required = Required.Always)]
            public bool free_avaliable;
            [JsonIgnore]
            public string hardsubLang {
                get {
                    return stream_data["hardsub_lang"].ToObject<string>();
                }
            }
            [JsonIgnore]
            public string audioLang {
                get {
                    return stream_data["audio_lang"].ToObject<string>();
                }
            }

            private bool StreamContainsQuality(string quality) {
                return stream_data["streams"]
                    .ToObject<JArray>().Where((a) => a["quality"].ToObject<string>() == quality)
                    ?.Count() > 0;
            }

            private string GetStream(string quality) {
                try {
                    return stream_data["streams"]
                        .ToObject<JArray>()
                        .Where((a) => a["quality"].ToObject<string>() == quality)
                        .FirstOrDefault()["url"]
                        .ToObject<string>();
                } catch {
                    Console.WriteLine(stream_data.ToString());
#if DEBUG
                    throw;
#endif
                }
                return null;
            }

            [JsonIgnore]
            public string BestStreamQuality {
                get {
                    if (StreamContainsQuality("ultra"))
                        return "ultra";
                    if (StreamContainsQuality("high"))
                        return "high";
                    if (StreamContainsQuality("mid"))
                        return "mid";
                    if (StreamContainsQuality("low"))
                        return "low";
                    return "adaptive";
                }
            }

            [JsonIgnore]
            public string BestStream {
                get {
                    return GetStream(BestStreamQuality);
                }
            }
            [JsonProperty(PropertyName = "stream_data", Required = Required.Always)]
            private JObject stream_data;
            [JsonProperty(PropertyName = "name")]
            public string episode_name;
            public string series_name;
            [JsonProperty(PropertyName = "collection_name")]
            public string season_name;
            public string series_id;
            [JsonProperty(PropertyName = "collection_id")]
            public string season_id;
        }

        public StreamInfo GetStreamInfo(Episode episode) => GetStreamInfo(episode.episode_id);
        public StreamInfo GetStreamInfo(string episode_id) {
            //Assert(!string.IsNullOrEmpty(episode_id));
            //Assert(!string.IsNullOrEmpty(_session_id));
            string postData = BuildPOSTRequest(new Dictionary<string, string>()
            {
                    { "media_id", episode_id },
                    { "fields", "media.class,media.media_id,media.etp_guid,media.collection_id,media.series_id,media.series_etp_guid,media.media_type,media.episode_number,media.name,media.collection_name,media.series_name,media.hardsub_lang,media.audio_lang,media.playhead,media.duration,media.screenshot_image,media.bif_url,media.url,media.stream_data,media.ad_spots,media.clip,media.sample,media.premium_only,media.available,media.premium_available,media.free_available,media.available_time,media.unavailable_time,media.premium_available_time,media.premium_unavailable_time,media.free_available_time,media.free_unavailable_time,media.availability_notes,media.created,media.mature" },
                });
            string ret = POST("info", postData);
            //Console.WriteLine(ret);
            if (string.IsNullOrEmpty(ret))
                return null;
            JObject node = JObject.Parse(ret);
            if (node["error"].ToObject<bool>())
                throw new Exception($"{node["code"]}: {node["message"]}");
            return node["data"].ToObject<StreamInfo>();
        }

        public Episode[] GetEpisodes(Season season, string sort = "asc") => GetEpisodes(season.season_id, sort);
        public Episode[] GetEpisodes(string season_id, string sort = "asc") {
            //Assert(!string.IsNullOrEmpty(season_id));
            //Assert(!string.IsNullOrEmpty(_session_id));
            string postData = BuildPOSTRequest(new Dictionary<string, string>()
            {
                    { "collection_id", season_id },
                    { "offset", "0" },
                    { "limit", "50000" },
                    { "sort", sort },
                    { "include_clips", "false" },
                    { "fields", "media.stream_data,media.media_id,media.name,media.series_id,media.premium_only,media.screenshot_image,media.premium_available,media.episode_number,media.duration,media.playhead" },

                });
            string ret = POST("list_media", postData);
            //Console.WriteLine(ret);
            if (string.IsNullOrEmpty(ret))
                return null;
            JObject node = JObject.Parse(ret);
            if (node["error"].ToObject<bool>())
                throw new Exception($"{node["code"]}: {node["message"]}");
            return node["data"].ToObject<Episode[]>();
        }

        public Season[] GetSeasons(SearchResult result) => GetSeasons(result.series_id);
        public Season[] GetSeasons(string series_id) {
            //Assert(!string.IsNullOrEmpty(series_id));
            //Assert(!string.IsNullOrEmpty(_session_id));
            string postData = BuildPOSTRequest(new Dictionary<string, string>()
            {
                    { "series_id", series_id },
                    { "offset", "0" },
                    { "limit", "50000" }
                });
            string ret = POST("list_collections", postData);
            if (string.IsNullOrEmpty(ret))
                return null;
            JObject node = JObject.Parse(ret);
            if (node["error"].ToObject<bool>())
                throw new Exception($"{node["code"]}: {node["message"]}");
            return node["data"].ToObject<Season[]>();
        }

        public SearchResult[] Search(string q) {
            //Assert(!string.IsNullOrEmpty(_session_id));
            //Assert(!string.IsNullOrEmpty(q));
            string postData = BuildPOSTRequest(new Dictionary<string, string>()
            {
                    { "q", q },
                    { "media_types", "anime" },
                    { "classes", "series" },
                    { "offset", "0" },
                    { "limit", "100" },
                    { "fields", "series.series_id,series.name,series.portrait_image,series.premium_only,series.media_count,series.collection_count" }
                });
            string ret = POST("search", postData);
            if (string.IsNullOrEmpty(ret))
                return null;
            JObject node = JObject.Parse(ret);
            if (node["error"].ToObject<bool>())
                throw new Exception($"{node["code"]}: {node["message"]}");
            return node["data"].ToObject<SearchResult[]>();
        }
        public bool CreateSession(string locale) {
            if (!string.IsNullOrWhiteSpace(_session_id)) {
                return true;
            }
            try {
                _locale = locale;
                string POSTData = BuildPOSTRequest(new Dictionary<string, string>() {
                        { "device_id", "00000000-4d19-2528-ffff-ffff99d603a9" }, //HWID?
                        { "device_type", "com.crunchyroll.windows.desktop" }, //App Name
                        { "access_token", "LNDJgOit5yaRIWN" } //Access token is found in the applications themselves!
                    });
                string result = POST("start_session", POSTData);
                JObject obj = JObject.Parse(result);
                if ((bool)obj["error"] == false) {
                    _session_id = (string)obj["data"]["session_id"];
                } else {
                    Console.WriteLine(result);
                    return false;
                }
                return true;
            } catch (Exception ex) {
                //Console.WriteLine(ex.ToString());
#if DEBUG
                throw;
#endif
            }
            return false;
        }
    }
}
