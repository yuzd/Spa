using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JavaScriptViewEngine.Utils;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Jint;
using Jint.CommonJS;
using Jint.Native;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json.Linq;
using NLog;
using RazorLight;
using spa.JavaScriptViewEngine.Utils;
using spa.Models;
using spa.Plugins;

namespace JavaScriptViewEngine
{
    public class RazorEngineOption
    {

    }

    public class RazorEngineBuilder : IRenderEngineBuilder
    {
        private readonly RazorRenderEngine RazorRenderEngine;

        public RazorEngineBuilder(IWebHostEnvironment hostingEnvironment, IOptions<RazorEngineOption> options)
        {
            RazorRenderEngine = new RazorRenderEngine(hostingEnvironment);
        }

        public IRenderEngine Build()
        {
            return RazorRenderEngine;
        }
    }

    public class RazorRenderEngine : IRenderEngine
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly RazorLightEngine _engine;
        private static readonly Logger logger;

        /// <summary>
        /// jint
        /// </summary>
        private static readonly Jint.Engine engine = new Jint.Engine(cfg => cfg.AllowClr());


        /// <summary>
        /// global appsettions.json配置文件内容
        /// </summary>
        private Dictionary<string, string> _appsettingsJson = new Dictionary<string, string>();

        /// <summary>
        /// current appsettions.json配置文件内容
        /// </summary>
        private Dictionary<string, string> _currentAppsettingsJson = new Dictionary<string, string>();

        /// <summary>
        /// 记录razor的缓存
        /// </summary>
        private ConcurrentDictionary<string, string> cacheList = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// global appsettions.json配置文件最后更新时间
        /// </summary>
        private DateTime? _appJsonLastWriteTime;

        /// <summary>
        /// current appsettions.json配置文件最后更新时间
        /// </summary>
        private DateTime? _currentAppJsonLastWriteTime;

        static RazorRenderEngine()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        public RazorRenderEngine(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            _engine = new RazorLightEngineBuilder()
                .DisableEncoding()
                .UseEmbeddedResourcesProject(typeof(RazorRenderEngine))
                .UseMemoryCachingProvider()
                .Build();
        }


        public async Task<RenderResult> RenderAsync(string path, object model, dynamic viewBag, RouteValueDictionary routevalues, string area,
            ViewType viewType)
        {
            var re = new RenderResult
            {
                Html = "",
                Status = 200
            };

            if (string.IsNullOrEmpty(path) || path.Equals("/")) return re;
            var nomarlPath = path;
            try
            {
                if (routevalues != null && routevalues.ContainsKey("url"))
                {
                    path = routevalues["url"].ToString();
                }

                //只拿第一层路径
                var entryPointName = path.Split('/').FirstOrDefault();
                if (string.IsNullOrEmpty(entryPointName))
                {
                    return re;
                }

                entryPointName = entryPointName.ToLowerInvariant();
                var indexHtml = new FileModel(_hostingEnvironment, entryPointName, "index.html");
                if (!indexHtml.IsExist)
                {
                    return re;
                }

                CheckConfigRefresh();
                CheckConfigRefresh(entryPointName);

                var html = indexHtml.GetContent();
                re.Html = html;

                var cacheKey = entryPointName + "_" + indexHtml.LastModifyTime.ToString("yyyMMddHHmmss");

                var jsFileContent = new FileModel(_hostingEnvironment, entryPointName, "_server_.js");
                dynamic serverJsResult = null;
                if (jsFileContent.IsExist)
                {
                    var exports = engine
                        .CommonJS()
                        .RegisterInternalModule("server", typeof(PluginFactory))
                        .RunMain(jsFileContent.FilePath);

                    try
                    {
                        var jsValue = exports.AsObject().Get("main").Invoke(new JsValue(nomarlPath)).ToString();
                        if (!string.IsNullOrEmpty(jsValue) && jsValue != "null" && jsValue != "undefined")
                        {
                            serverJsResult = JObject.Parse(jsValue);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error("excute _server_.js fail:" + e.Message);
                    }
                }

                if (serverJsResult == null)
                {
                    serverJsResult = new JObject();
                }
                serverJsResult.GlobalEnv = new JObject();
                if (_appsettingsJson != null)
                {
                    foreach (var jsonItem in _appsettingsJson)
                    {
                        serverJsResult.GlobalEnv[jsonItem.Key] = jsonItem.Value;
                    }
                }
                serverJsResult.Env = new JObject();
                if (_currentAppsettingsJson != null)
                {
                    foreach (var jsonItem in _currentAppsettingsJson)
                    {
                        serverJsResult.Env[jsonItem.Key] = jsonItem.Value;
                    }
                }
                try
                {
                    var cacheResult = _engine.Handler.Cache.RetrieveTemplate(cacheKey);
                    if (cacheResult.Success)
                    {
                        var itemple = cacheResult.Template.TemplatePageFactory();
                        itemple.DisableEncoding = true;
                        string result2 = await _engine.RenderTemplateAsync(itemple, serverJsResult);
                        re.Html = result2;
                        return re;
                    }

                    string result = await _engine.CompileRenderStringAsync(cacheKey, html, serverJsResult);
                    if (!cacheList.TryGetValue(entryPointName, out var oldCache))
                    {
                        cacheList.TryAdd(entryPointName, cacheKey);
                    }
                    else
                    {
                        //之前有缓存了 就清掉
                        _engine.Handler.Cache.Remove(oldCache);
                        cacheList[entryPointName] = cacheKey;
                    }

                    re.Html = result;
                }
                catch (Exception e)
                {
                    logger.Error(e.ToString());
                }
            }
            catch (Exception e1)
            {
                //ignore
                logger.Error(e1.ToString());
            }

            return re;
        }

        private void CheckConfigRefresh(string projectName = null)
        {
            var jsonFile = string.IsNullOrEmpty(projectName) ? new FileInfo(Path.Combine(_hostingEnvironment.WebRootPath, ConfigHelper.DefaultAppSettingsFile)) :
                new FileInfo(Path.Combine(_hostingEnvironment.WebRootPath, projectName, ConfigHelper.DefaultAppSettingsFile));
            var jsonLastTime = string.IsNullOrEmpty(projectName) ? _appJsonLastWriteTime : _currentAppJsonLastWriteTime;
            if (jsonFile.Exists && (jsonLastTime == null || jsonLastTime != jsonFile.LastWriteTime))
            {
                try
                {
                    if (string.IsNullOrEmpty(projectName))
                    {
                        _appJsonLastWriteTime = jsonFile.LastWriteTime;
                        this._appsettingsJson =
                            Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(
                                CopyHelper.ReadAllText(jsonFile.FullName));
                    }
                    else
                    {
                        _currentAppJsonLastWriteTime = jsonFile.LastWriteTime;
                        this._currentAppsettingsJson =
                            Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(
                                CopyHelper.ReadAllText(jsonFile.FullName));
                    }
                }
                catch (Exception e)
                {
                    logger.Info(e.ToString());
                }
            }
        }

        public RenderResult Render(string path, object model, dynamic viewBag, RouteValueDictionary routevalues, string area, ViewType viewType)
        {
            return new RenderResult
            {
                Html = "",
                Status = 200
            };
        }

        public void Dispose()
        {
        }
    }

    public class SingletonRenderEngineFactory : IRenderEngineFactory
    {
        IRenderEngine _renderEngine;
        bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledRenderEngineFactory" /> class.
        /// </summary>
        /// <param name="renderEngineBuilder">The render engine builder.</param>
        public SingletonRenderEngineFactory(IRenderEngineBuilder renderEngineBuilder)
        {
            _renderEngine = renderEngineBuilder.Build();
        }

        /// <summary>
        /// Gets a <see cref="IRenderEngine" /> engine from the pool.
        /// </summary>
        /// <returns>
        /// The <see cref="IRenderEngine" />
        /// </returns>
        public virtual IRenderEngine RequestEngine()
        {
            EnsureValidState();
            return _renderEngine;
        }

        /// <summary>
        /// Returns an <see cref="IRenderEngine" /> to the pool so it can be reused
        /// </summary>
        /// <param name="engine">Engine to return</param>
        public virtual void ReturnEngine(IRenderEngine engine)
        {
            // no pooling
        }

        /// <summary>
        /// Dispose of the render engine
        /// </summary>
        public virtual void Dispose()
        {
            _disposed = true;
            _renderEngine?.Dispose();
            _renderEngine = null;
        }

        /// <summary>
        /// Ensures that this object isn't disposed
        /// </summary>
        /// <exception cref="ObjectDisposedException"></exception>
        public void EnsureValidState()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}