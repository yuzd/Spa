using System;
using System.IO;
using System.Linq;
using JavaScriptViewEngine;
using LogDashboard;
using LogDashboard.Authorization.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using spa.Controller;
using spa.Filter;
using spa.JavaScriptViewEngine.Utils;

namespace spa
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            ConfigHelper._configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            //采用开源的logger查看组件查看本地日志文件
            services.AddLogDashboard(opt =>
            {
                var localUserName = ConfigHelper.GetConfig("BasicAuth:Name") ;
                var localPassword = ConfigHelper.GetConfig("BasicAuth:Pwd");
                if (!string.IsNullOrEmpty(localUserName) && !string.IsNullOrEmpty(localPassword))
                {
                    opt.AddAuthorizationFilter(new LogDashboardBasicAuthFilter(localUserName, localPassword));
                }
            });

            services.AddJsEngine(builder =>
            {
                builder.UseRenderEngine<RazorEngineBuilder>();
                builder.UseSingletonEngineFactory();
            });

           
            services.AddCors(o => o.AddPolicy("Any", r =>
            {
                r.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }));

            services.AddRazorPages().AddNewtonsoftJson(options =>
                options.SerializerSettings.ContractResolver =
                    new DefaultContractResolver());

            services.AddHttpContextAccessor();

            services.AddScoped<BasicAuthFilter>();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory logging)
        {

            ConfigHelper.ContentRootPath = env.ContentRootPath;
            ConfigHelper.WebRootPath = env.WebRootPath;
            if (string.IsNullOrEmpty(ConfigHelper.WebRootPath))
            {
                ConfigHelper.WebRootPath = Path.Combine(ConfigHelper.ContentRootPath, "wwwroot");
            }

            ConfigHelper.BackupPath = Path.Combine(env.ContentRootPath, "backup");
#if DEBUG
            app.UseDeveloperExceptionPage();
#endif

            #region 对于敏感的文件不让访问

            //对于敏感的文件不让访问
            var fileExtentionNotAllowed = ConfigHelper.GetConfig("NotAllowedFileExtentions", "appsettings.json;.map").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            fileExtentionNotAllowed.Add("server.js");//服务端js代码 里面会有敏感信息
            app.UseWhen(
                c =>
                {
                    var currentRequestPath = ConfigHelper.GetFileNameByRequestPath(c.Request.Path.Value.ToLower());
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


            #region 针对apple开发的注意事项
            //针对apple开发的注意事项
            app.UseWhen(
                c =>
                {
                    if (c.Request.Path.Value.ToLower().EndsWith("apple-app-site-association")) return true;
                    if (!c.Request.Path.Value.Contains("www")) return false;
                    var staticFileNameArr = c.Request.Path.Value.Split(new string[] { "www/" }, StringSplitOptions.None);
                    if (staticFileNameArr.Length != 2) return false;
                    var isoFile = Path.Combine(env.WebRootPath, staticFileNameArr[1]);
                    return File.Exists(isoFile);
                },
                _ => _.Run(context =>
                    {
                        var fileName = "";
                        if (context.Request.Path.Value.ToLower().EndsWith("apple-app-site-association"))
                        {
                            fileName = "apple-app-site-association";
                        }
                        else
                        {
                            var staticFileNameArr = context.Request.Path.Value.Split(new string[] { "www/" }, StringSplitOptions.None);
                            fileName = staticFileNameArr[1];
                        }
                        var isoFile = Path.Combine(env.WebRootPath, fileName);
                        if (File.Exists(isoFile))
                        {
                            return context.Response.WriteAsync(File.ReadAllText(isoFile));
                        }
                        return context.Response.WriteAsync("404");
                    }
                ));


            #endregion

            //使用日志组件
            app.UseLogDashboard();

            //内部api
            app.UseWhen(
                c =>
                {
                    var path = c.Request.Path.Value.ToLower();
                    return path.EndsWith(".api") || path.EndsWith(".rollback") || path.EndsWith(".upload") || path.EndsWith(".delete") || path.EndsWith(".pathlist") || path.EndsWith(".reupload");
                },
                _ => _.UseMiddleware<ApiMiddleware>());


            #region 静态文件

            //静态文件
            app.UseStaticFiles();


            //增加配置静态文件的映射
            var fileExtention = Environment.GetEnvironmentVariable("AllowedFileExtentionMapping");//格式为 .plist->application/xml,.ipa->application/octet-stream
            if (!string.IsNullOrEmpty(fileExtention))
            {
                var provider = new FileExtensionContentTypeProvider();
                var fileExtentionArr = fileExtention.Split(',',StringSplitOptions.RemoveEmptyEntries);
                foreach (var arr in fileExtentionArr)
                {
                    var filePair = arr.Split(new string[] {"->"}, StringSplitOptions.RemoveEmptyEntries);
                    if(filePair.Length!=2)continue;
                    provider.Mappings[filePair[0]] = filePair[1];
                }
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
                    ContentTypeProvider = provider
                });
            }
            #endregion

            app.UseRouting();

            app.UseCors();

            app.UseJsEngine();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute(
                    "Admin",
                    "Admin/{*url}",defaults: new { controller = "Home", action = "Admin" });
                endpoints.MapControllerRoute(
                    name: "Spa",
                    pattern: "{*url}", defaults: new { controller = "Home", action = "Index" });

            });

        }

    }
}