using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace spa.Filter
{
    

    public class BasciAuth : Attribute, IFilterFactory
    {
        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var filter = serviceProvider.GetService<BasicAuthFilter>();
            return filter;
        }

        public bool IsReusable => false;
    }
  
    public class BasicAuthFilter : IActionFilter 
    {
        private readonly IConfiguration _configuration;

        public BasicAuthFilter(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public void OnActionExecuting(ActionExecutingContext context)
        {
            StringValues header = context.HttpContext.Request.Headers[nameof (Authorization)];
            if (string.IsNullOrWhiteSpace((string) header))
            {
                context.HttpContext.Response.Headers.Add("WWW-Authenticate", (StringValues) "BASIC realm=\"api\"");
                context.HttpContext.Response.StatusCode = 401;
                return ;
            }
            
            
            var authHeader = AuthenticationHeaderValue.Parse(header);
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
            var username = credentials[0];
            var password = credentials[1];
            var localUserName = _configuration["BasicAuth:Name"];
            var localPassword = _configuration["BasicAuth:Password"];
            if (!string.IsNullOrEmpty(localUserName) && !string.IsNullOrEmpty(localPassword) && (!username.Equals(localUserName) || !password.Equals(localPassword)))
            {
                context.HttpContext.Response.Headers.Add("WWW-Authenticate", (StringValues) "BASIC realm=\"api\"");
                context.HttpContext.Response.StatusCode = 401;
                return;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}