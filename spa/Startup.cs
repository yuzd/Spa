using System;
using JavaScriptViewEngine;
using LogDashboard;
using LogDashboard.Authorization.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                var name = Environment.GetEnvironmentVariable("BasicAuth_Name");
                var pwd = Environment.GetEnvironmentVariable("BasicAuth_Pwd");
                var localUserName =  string.IsNullOrEmpty(name)? _configuration["BasicAuth:Name"]: name;
                var localPassword = string.IsNullOrEmpty(pwd)? _configuration["BasicAuth:Password"]: pwd;
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
                    return path.EndsWith(".api") || path.EndsWith(".rollback") || path.EndsWith(".upload") || path.EndsWith(".delete") || path.EndsWith(".pathlist") || path.EndsWith(".reupload");
                },
                _ => _.UseMiddleware<ApiMiddleware>());

            app.UseStaticFiles();

            app.UseJsEngine();

            app.UseMvc(routes =>
            {
                routes.MapRoute("Admin", "Admin/{*url}", defaults: new { controller = "Home", action = "Admin" });
                routes.MapRoute("Spa", "{*url}", defaults: new { controller = "Home", action = "Index" });
            });

        }

    }
}