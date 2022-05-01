using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JavaScriptViewEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using spa.Asset;
using spa.Controller;
using spa.JavaScriptViewEngine;
using spa.JavaScriptViewEngine.Middleware;
using spa.JavaScriptViewEngine.Utils;

namespace spa.Filter
{
    public static class FilterExtention
    {
        private static readonly IDictionary<string, EmbeddedAssetDescriptor> _pathToAssetMap = new ConcurrentDictionary<string, EmbeddedAssetDescriptor>();
        public static IApplicationBuilder UseSpa(this IApplicationBuilder app)
        {
            //对于敏感的文件不让访问
            app.UseNotAllowedFileFilter();

            app.UseEmbeddedAsset();

            app.UseEmbeddedAdmin();
            app.UseEmbeddedCasBin();

            //内部api
            app.UseWhen(
                c =>
                {
                    var path = c.Request.Path.Value!.ToLower();
                    return path.EndsWith(".api") || path.EndsWith(".rollback") || path.EndsWith(".upload") || path.EndsWith(".delete") || path.EndsWith(".pathlist") || path.EndsWith(".reupload");
                },
                _ => _.UseMiddleware<ApiMiddleware>());

            //扩展静态文件访问
            app.UseAllowedFileFilter();

            //使用js引擎
            app.UseMiddleware<RenderEngineMiddleware>();

            var wwwrootAppsettingJson = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.DefaultAppSettingsFile);
            if (!File.Exists(wwwrootAppsettingJson))
            {
                File.WriteAllText(wwwrootAppsettingJson,"{}");
            }

            return app;

        }

        public static IServiceCollection AddSpa(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<RazorRenderEngine>();
            return services;
        }

        /// <summary>
        /// 如果请求/asset/的文件
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseEmbeddedAsset(this IApplicationBuilder app)
        {
            var assetList = new EmbeddedAssetProvider(_pathToAssetMap);
            var thisAssembly = typeof(FilterExtention).Assembly;
            foreach (var resourceName in thisAssembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith("spa.Asset")) continue; // original assets only

                var path = resourceName.Replace("\\", "/");

                _pathToAssetMap[path.ToLower()] = new EmbeddedAssetDescriptor(thisAssembly, resourceName, false);
            }

            return app.UseWhen(
                c =>
                {
                    var url = c.Request.Path.Value!.ToLower();
                    return url.Contains("/asset/");
                },
                _ => _.Run((async context =>
                {
                    using var webAsset = assetList.GetAsset(context.Request.Path.Value.ToLower());
                    context.Response.ContentType = webAsset.MediaType;
                    await webAsset.Stream.CopyToAsync(context.Response.Body);
                })));
        }

        public static IApplicationBuilder UseEmbeddedAdmin(this IApplicationBuilder app)
        {
            var assetList = new EmbeddedAssetProvider(_pathToAssetMap);

            return app.UseWhen(
                c =>
                {
                    var url = c.Request.Path.Value!.ToLower();
                    return url.EndsWith("spa.admin");
                },
                _ => _.Run((async context =>
                {
                    var authCheck = ApiMiddleware.AuthCheck(context, true);
                    if (!authCheck)
                    {
                        return;
                    }
                    using var webAsset = assetList.GetAsset("/spa/asset/admin/admin.html");
                    context.Response.ContentType = webAsset.MediaType;
                    await webAsset.Stream.CopyToAsync(context.Response.Body);
                })));
        }

        public static IApplicationBuilder UseEmbeddedCasBin(this IApplicationBuilder app)
        {
            var assetList = new EmbeddedAssetProvider(_pathToAssetMap);

            return app.UseWhen(
                c =>
                {
                    var url = c.Request.Path.Value!.ToLower();
                    return url.EndsWith("spa.casbin");
                },
                _ => _.Run((async context =>
                {
                    var authCheck = ApiMiddleware.AuthCheck(context, true);
                    if (!authCheck)
                    {
                        return;
                    }
                    using var webAsset = assetList.GetAsset("/spa/asset/editor/casbin.html");
                    context.Response.ContentType = webAsset.MediaType;
                    await webAsset.Stream.CopyToAsync(context.Response.Body);
                })));
        }

        /// <summary>
        /// 对于敏感的文件不让访问
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseNotAllowedFileFilter(this IApplicationBuilder app)
        {
            #region 对于敏感的文件不让访问

            //对于敏感的文件不让访问
            var fileExtentionNotAllowed = ConfigHelper.GetConfig("NotAllowedFileExtentions", "appsettings.json;_appsettings_.json;.map").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            fileExtentionNotAllowed.Add("_appsettings_.json");
            fileExtentionNotAllowed.Add("_server_.js");//服务端js代码 里面会有敏感信息
            return app.UseWhen(
                c =>
                {
                    var path = c.Request.Path.Value!.ToLower();
                    if(path.Contains("/_backup_/"))
                    {
                        return true;
                    }
                    var currentRequestPath = ConfigHelper.GetFileNameByRequestPath(path);
                    foreach (var notallowed in fileExtentionNotAllowed)
                    {
                        if (notallowed.StartsWith(".") && currentRequestPath.EndsWith(notallowed.ToLower()))
                        {
                            //匹配文件后缀
                            return true;
                        }

                        if (currentRequestPath.Equals(notallowed.ToLower()))
                        {
                            return true;
                        }
                    }

                    return false;
                },
                _ => _.Run((context => context.Response.WriteAsync("503"))));

            #endregion
        }

        /// <summary>
        /// 扩展静态文件访问
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseAllowedFileFilter(this IApplicationBuilder app)
        {
            #region 静态文件

            #region 针对apple开发的注意事项
            //针对apple开发的注意事项
            app.UseWhen(
                c =>
                {
                    if (c.Request.Path.Value.ToLower().EndsWith("apple-app-site-association")) return true;
                    if (!c.Request.Path.Value.Contains("www")) return false;
                    var staticFileNameArr = c.Request.Path.Value.Split(new string[] { "www/" }, StringSplitOptions.None);
                    if (staticFileNameArr.Length != 2) return false;
                    var isoFile = Path.Combine(ConfigHelper.WebRootPath, staticFileNameArr[1]);
                    return File.Exists(isoFile);
                },
                _ => _.Run(context =>
                    {
                        var fileName = "";
                        if (context.Request.Path.Value!.ToLower().EndsWith("apple-app-site-association"))
                        {
                            fileName = "apple-app-site-association";
                        }
                        else
                        {
                            var staticFileNameArr = context.Request.Path.Value.Split(new string[] { "www/" }, StringSplitOptions.None);
                            fileName = staticFileNameArr[1];
                        }
                        var isoFile = Path.Combine(ConfigHelper.WebRootPath, fileName);
                        if (File.Exists(isoFile))
                        {
                            return context.Response.WriteAsync(File.ReadAllText(isoFile));
                        }
                        return context.Response.WriteAsync("404");
                    }
                ));


            #endregion

            //静态文件
            app.UseStaticFiles();


            //增加配置静态文件的映射
            var fileExtention = Environment.GetEnvironmentVariable("AllowedFileExtentionMapping");//格式为 .plist->application/xml,.ipa->application/octet-stream
            if (!string.IsNullOrEmpty(fileExtention))
            {
                var provider = new FileExtensionContentTypeProvider();
                var fileExtentionArr = fileExtention.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var arr in fileExtentionArr)
                {
                    var filePair = arr.Split(new string[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                    if (filePair.Length != 2) continue;
                    provider.Mappings[filePair[0]] = filePair[1];
                }
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
                    ContentTypeProvider = provider
                });
            }
            #endregion

            return app;
        }

        public static string GetRawUrl(this HttpRequest request)
        {
            // string str1 = request.Scheme ?? string.Empty;
            // string str2 = request.Host.Value ?? string.Empty;
            // PathString pathString = request.PathBase;
            // string str3 = pathString.Value ?? string.Empty;
            PathString pathString = request.Path;
            string str4 = pathString.Value ?? string.Empty;
            string str5 = request.QueryString.Value ?? string.Empty;
            return new StringBuilder().Append(str4).Append(str5).ToString();
        }

    }
}
