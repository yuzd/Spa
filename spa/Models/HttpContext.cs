using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HttpClientFactory.Impl;

namespace spa.Models
{
    public class HttpContext
    {
        private readonly Dictionary<string, string> _paramDic;
        private readonly Dictionary<string, string> _headerDic;


        public HttpContext(object param)
        {
            var dd = getPrams(param);
            _paramDic = dd.Item1;
            _headerDic = dd.Item2;
        }

        public string request()
        {
            return request(null);
        }

        public string request(object param)
        {
            var dd = getPrams(param);
            var dataDic = dd.Item1;
            var dataHDic = dd.Item2;
            foreach (var p in _paramDic)
            {
                if (!dataDic.ContainsKey(p.Key))
                {
                    dataDic.Add(p.Key, p.Value);
                }
            }

            foreach (var p in _headerDic)
            {
                if (!dataHDic.ContainsKey(p.Key))
                {
                    dataHDic.Add(p.Key, p.Value);
                }
            }


            if (!dataDic.TryGetValue("url", out var url))
            {
                return "url can not be null";
            }

            if (!dataDic.TryGetValue("method", out var method))
            {
                method = "get";
            }

            if (!dataDic.TryGetValue("data", out var data))
            {
                data = string.Empty;
            }

            if (!dataDic.TryGetValue("contentType", out var contentType))
            {
                contentType = "application/json";
            }

            if (!dataDic.TryGetValue("timeout", out var timeout))
            {
                timeout = "5000";
            }

            if (!dataDic.TryGetValue("basicUserName", out var basicUserName))
            {
                basicUserName = string.Empty;
            }

            if (!dataDic.TryGetValue("basicPassword", out var basicPassword))
            {
                basicPassword = string.Empty;
            }

            var client = HangfireHttpClientFactory.Instance.GetHttpClient(url);

            var request = new HttpRequestMessage(new HttpMethod(method), url);

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));
            if (!method.ToLower().Equals("get"))
            {
                if (!string.IsNullOrEmpty(data))
                {
                    request.Content = new StringContent(data, Encoding.UTF8, contentType);
                }
            }

            foreach (var header in dataHDic)
            {
                if (string.IsNullOrEmpty(header.Key)) continue;
                request.Headers.Add(header.Key, header.Value);
            }

            if (!string.IsNullOrEmpty(basicPassword) && !string.IsNullOrEmpty(basicUserName))
            {
                var byteArray = Encoding.ASCII.GetBytes(basicUserName + ":" + basicPassword);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }


            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(int.Parse(timeout)));

            var httpResponse = client.SendAsync(request, cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();

            HttpContent content = httpResponse.Content;
            string result = content.ReadAsStringAsync().GetAwaiter().GetResult();

            return result;
        }

        private Tuple<Dictionary<string, string>, Dictionary<string, string>> getPrams(object param)
        {
            Dictionary<string, string> paramDic = new Dictionary<string, string>();
            Dictionary<string, string> headerDic = new Dictionary<string, string>();

            if (param != null && param is ExpandoObject properties)
            {
                foreach (var property in properties)
                {
                    if (property.Value is ExpandoObject header)
                    {
                        foreach (var h in header)
                        {
                            headerDic.Add(h.Key, h.Value.ToString());
                        }
                    }
                    else
                    {
                        paramDic.Add(property.Key, property.Value.ToString());
                    }

                }
            }
            return new Tuple<Dictionary<string, string>, Dictionary<string, string>>(paramDic, headerDic);
        }
    }


    internal class HangfireHttpClientFactory : PerHostHttpClientFactory
    {
        internal static readonly HangfireHttpClientFactory Instance = new HangfireHttpClientFactory();

        protected override HttpClient CreateHttpClient(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.ConnectionClose = false;
            client.DefaultRequestHeaders.Add("UserAgent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/47.0.2526.106 Safari/537.36");
            client.Timeout = TimeSpan.FromHours(1);//这里设置1小时 是为了取消HttpClient自带的默认超时100s的限制 会在业务逻辑里面设使用实际的Timeout
            return client;
        }

        protected override HttpMessageHandler CreateMessageHandler(string proxyUrl = null)
        {
            var handler = new HttpClientHandler();
            if (string.IsNullOrEmpty(proxyUrl))
            {
                handler.UseProxy = false;
            }
            else
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(proxyUrl);
            }

            handler.AllowAutoRedirect = false;
            handler.AutomaticDecompression = DecompressionMethods.None;
            handler.UseCookies = false;
            return handler;
        }
    }
}
