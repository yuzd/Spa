using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using spa.Models;
using spa.Utils;

namespace spa.Domain
{
    /// <summary>
    /// api
    /// </summary>
    public class ApiMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Logger logger;

        static ApiMiddleware()
        {
            logger = LogManager.GetCurrentClassLogger();
        }
        public ApiMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                if (!CanInvoke(context, out var route))
                {
                    await context.Response.WriteAsync("action is null");
                    return;
                }

                var spaUser = AuthCheck(context);
                if (spaUser == null) return;
                context.Items["spaUser"] = spaUser;

                var domain = context.RequestServices.GetService<SpaDomain>();
                var project = route.Item1;
                var api = route.Item2;
                var methodInfo = domain?.getApi(api) ?? null;
                if (methodInfo == null)
                {
                    await context.Response.WriteAsync("invaild api");
                }
                
                var spaAttribute = methodInfo!.GetCustomAttribute<SpaApi>();
                if (!string.IsNullOrEmpty(spaAttribute!.SupperRequired) && ((spaAttribute!.SupperRequired == "*" ) || spaAttribute.SupperRequired.ToLower() == project)  && !spaUser.Supper)
                {
                    // 只能supper访问
                    context.Response.StatusCode = 403;
                    return;
                }

                // supper可以无限制访问
                if (spaUser.Supper || spaAttribute.NotCheck)
                {
                    await ((Task)methodInfo.Invoke(domain, new object[] { project, context }))!;
                    return;
                }
                
                // 普通用户看supper有没有给他权限访问
                var e = ConfigHelper.createEnforcer();
                var auth = e.Auth(spaUser.LoginName, project, "/"+api);
                if (!auth)
                {
                    context.Response.StatusCode = 403;
                    return;
                }
                await ((Task)methodInfo.Invoke(domain, new object[] { project, context }))!;
            }
            catch (Exception e)
            {
                await context.Response.WriteAsync(e.ToString());
            }
        }

        /// <summary>
        /// 是否符合地址规则
        /// </summary>
        /// <param name="context"></param>
        /// <param name="apiName"></param>
        /// <returns></returns>
        internal static bool CanInvoke(HttpContext context, out Tuple<String, String> apiName)
        {
            apiName = new Tuple<string, string>("", "");
            var path = context.Request.Path.Value!.ToLower();
            var action = path.Split('/').LastOrDefault();
            if (string.IsNullOrEmpty(action))
            {
                return false;
            }

            action = action.ToLower();
            var arr = action.Split('.');
            if (arr.Length != 2)
            {
                return false;
            }

            apiName = new Tuple<string, string>(arr[0], arr[1]);
            return true;
        }

        /// <summary>
        /// 验证auth
        /// </summary>
        /// <param name="context"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static SpaUser AuthCheck(HttpContext context, string path = null)
        {
            var isHtml = !String.IsNullOrEmpty(path);
            if (!context.Request.Headers.ContainsKey(nameof(Authorization)))
            {
                context.Response.StatusCode = 403;
                if (isHtml)
                {
                    context.Response.Headers.Add("WWW-Authenticate", (StringValues)"BASIC realm=\"api\"");
                    context.Response.StatusCode = 401;
                }

                return null;
            }

            var authHeader = AuthenticationHeaderValue.Parse(context.Request.Headers[nameof(Authorization)]);
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
            var username = credentials[0];
            var password = credentials[1];
            var localUserName = ConfigHelper.GetConfig("SuperBasicAuth:Name");
            var localPassword = ConfigHelper.GetConfig("SuperBasicAuth:Pwd");
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                context.Response.StatusCode = 403;
                if (isHtml)
                {
                    context.Response.Headers.Add("WWW-Authenticate", (StringValues)"BASIC realm=\"api\"");
                    context.Response.StatusCode = 401;
                }

                return null;
            }

            //是否是上帝模式的用户
            if ((username.Equals(localUserName) &&
                 password.Equals(localPassword)))
            {
                return new SpaUser
                {
                    Supper = true,
                    LoginName = username
                };
            }
            

            if (isHtml && path.ToLower() == "casbin")
            {
                // casbin 管理页面只能是上帝模式的用户
                context.Response.StatusCode = 403;
                context.Response.WriteAsync("Unauthorized(401)");
                return null;
            }

            // 接口权限验证
            var authFile = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.CasBinUserSettingsFile);
            if (File.Exists(authFile))
            {
                var userDic = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, String>>(File.ReadAllText(authFile));
                if (userDic != null && userDic.TryGetValue(username, out var pss) && password.Equals(pss))
                {
                    return new SpaUser
                    {
                        LoginName = username
                    };
                }
            }

            if (isHtml)
            {
                context.Response.Headers.Add("WWW-Authenticate", (StringValues)"BASIC realm=\"api\"");
                context.Response.StatusCode = 401;
            }
            return null;
        }
    }
}