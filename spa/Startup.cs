using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JavaScriptViewEngine;
using LogDashboard;
using LogDashboard.Authorization.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NLog.Extensions.Logging;
using spa.Controller;
using spa.Filter;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace spa
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            
            services.AddLogDashboard(opt =>
            {
                var localUserName = _configuration["BasicAuth:Name"];
                var localPassword = _configuration["BasicAuth:Password"];
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

            services.AddScoped<BasicAuthFilter>();
            services.AddMvc();
        }

       

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory logging)
        {
            #region AntORM

            app.UseAntData();

            #endregion
            #region NLOG

            NLog.LogManager.LoadConfiguration("nlog.config");
            logging.AddNLog();

            #endregion
            
            app.UseDeveloperExceptionPage();
            
            app.UseWhen(
                c => c.Request.Path.Value.ToLower().EndsWith("appsettings.json") || c.Request.Path.Value.ToLower().EndsWith(".map"),
                _ => _.Run((context => context.Response.WriteAsync("503"))));
            
            app.UseLogDashboard();
      
            app.UseWhen(
                c =>
                {
                    var path = c.Request.Path.Value.ToLower();
                    return path.EndsWith(".api") || path.EndsWith(".rollback") || path.EndsWith(".upload") || path.EndsWith(".delete")|| path.EndsWith(".pathlist") || path.EndsWith(".reupload");
                },
                _ => _.UseMiddleware<ApiMiddleware>());
            
            app.UseStaticFiles();

            app.UseJsEngine();
            
            app.UseMvc(routes =>
            {
                routes.MapRoute("Admin", "Admin/{*url}", defaults: new {controller = "Home", action = "Admin"});
                routes.MapRoute("Spa", "{*url}", defaults: new {controller = "Home", action = "Index"});
            });
            
            var redisConnection = _configuration["RedisConnection"];
            var csredis = new CSRedis.CSRedisClient(redisConnection);
            //初始化 Cache
            RedisHelper.Initialization(csredis);
        }

    }
}