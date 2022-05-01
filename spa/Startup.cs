using System;
using System.IO;
using System.Linq;
using LogDashboard;
using LogDashboard.Authorization.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using spa.Filter;
using spa.Utils;

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
            //采用开源的logger查看组件查看本地日志文件
            services.AddLogDashboard(opt =>
            {
                var localUserName = _configuration["BasicAuth:Name"];
                var localPassword = _configuration["BasicAuth:Pwd"];
                if (!string.IsNullOrEmpty(localUserName) && !string.IsNullOrEmpty(localPassword))
                {
                    opt.AddAuthorizationFilter(new LogDashboardBasicAuthFilter(localUserName, localPassword));
                }
            });

            services.AddSpa();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory logging)
        {
#if DEBUG
            app.UseDeveloperExceptionPage();
#endif
            //使用开源的本地日志组件
            app.UseLogDashboard();

            app.UseSpa(env, _configuration);
        }
    }
}