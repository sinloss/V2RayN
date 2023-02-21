using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using v2rayN.Mode;
using v2rayN.Properties;

namespace v2rayN.HttpProxyHandler
{
    /// <summary>
    /// 提供PAC功能支持
    /// </summary>
    class PACListHandle
    {
        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;

        public class ResultEventArgs : EventArgs
        {
            public bool Success;

            public ResultEventArgs(bool success)
            {
                this.Success = success;
            }
        }

        private const string GFWLIST_URL = "https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt";

        private static readonly IEnumerable<char> IgnoredLineBegins = new[] { '!', '[' };

        private static Regex RoutePattern = new Regex(@"([a-z]*:?)([a-zA-Z0-9\.-]+).*");

        public void UpdatePACFromGFWList(Config config)
        {
            string url = GFWLIST_URL;
            if (!Utils.IsNullOrEmpty(config.urlGFWList))
            {
                url = config.urlGFWList;
            }

            //默认用户已开启系统代理
            //var httpProxy = config.inbound.FirstOrDefault(x => x.protocol=="http");
            //if (httpProxy == null)
            //{
            //    throw new Exception("未发现HTTP代理，无法设置代理更新");
            //}
            WebClient http = new WebClient();
            //http.Headers.Add("Connection", "Close");
            //http.Proxy = new WebProxy(IPAddress.Loopback.ToString(), httpProxy.localPort);
            http.DownloadStringCompleted += delegate (object sender, DownloadStringCompletedEventArgs e)
            {
                http_DownloadStringCompleted(sender, e, config);
            };
            http.DownloadStringAsync(new Uri(url));
        }

        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e, Config config = null)
        {
            try
            {
                File.WriteAllText(Utils.GetTempPath("gfwlist.txt"), e.Result, Encoding.UTF8);
                List<string> lines = ParseResult(e.Result, config);
                string abpContent = Utils.UnGzip(Resources.abp_js);
                abpContent = abpContent.Replace("__RULES__", JsonConvert.SerializeObject(lines, Formatting.Indented));
                File.WriteAllText(Utils.GetPath(Global.pacFILE), abpContent, Encoding.UTF8);
                if (UpdateCompleted != null) UpdateCompleted(this, new ResultEventArgs(true));
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);

                if (Error != null) Error(this, new ErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// 从config中提取必要信息，并合并到pac的规则中
        /// </summary>
        /// <param name="lines">pac规则列表</param>
        /// <param name="config">配置信息</param>
        /// <returns>需要从PAC中剔除的规则</returns>
        private static List<Regex> merge(List<string> lines, Config config)
        {
            List<Regex> cull = new List<Regex>();
            if (lines != null && config != null)
            {
                Cull(cull, config.userdirect, null);
                Cull(cull, config.useragent, (mode, host) =>
                {
                    switch (mode)
                    {
                        case "domain:":
                            lines.Add(@"||" + host + @"^"); // matches exactly itself
                            lines.Add("." + host + @"^"); // matches sub domain
                            break;
                        case "regex:":
                            lines.Add(@"\" + host + @"\");
                            break;
                        case "full:":
                            lines.Add(@"||" + host);
                            break;
                        default:
                            lines.Add("*" + host + "*");
                            break;
                    }
                });
            }
            return cull;
        }

        private static void Cull(List<Regex> cull, List<string> entries, Action<string, string> and)
        {
            foreach (string u in entries)
            {
                string ua = u.Trim();
                if (ua.StartsWith("geoip:") || ua.StartsWith("geosite:")
                    || ua.StartsWith("ext:")) continue;
                Match m = RoutePattern.Match(ua);
                if (m.Success)
                {
                    GroupCollection g = m.Groups;
                    string mode = g[1].Value;
                    string host = g[2].Value;
                    switch (mode)
                    {
                        case "domain:":
                            cull.Add(new Regex(@".+" + @host));
                            break;
                        case "regex:":
                            cull.Add(new Regex(@".*" + host));
                            break;
                        case "full:":
                            cull.Add(new Regex(@"[\|https:]*" + @host));
                            break;
                        default:
                            cull.Add(new Regex(@".*" + @host + ".*"));
                            break;
                    }
                    and?.Invoke(mode, host);
                }
            }
        }

        public static List<string> ParseResult(string response, Config config)
        {
            byte[] bytes = Convert.FromBase64String(response);
            string content = Encoding.ASCII.GetString(bytes);
            List<string> valid = new List<string>();
            List<Regex> cull = merge(valid, config);
            using (var sr = new StringReader(content))
            {
                foreach (var line in sr.NonWhiteSpaceLines())
                {
                    if (line.BeginWithAny(IgnoredLineBegins) || !should(cull, line))
                        continue;
                    valid.Add(line);
                }
            }
            return valid;
        }

        public static bool should(List<Regex> cull, String line)
        {
            foreach (Regex reg in cull)
            {
                if (reg.IsMatch(line))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
