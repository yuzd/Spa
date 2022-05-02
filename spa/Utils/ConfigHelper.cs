using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NetCasbin;
using NetCasbin.Persist;
using NetCasbin.Persist.FileAdapter;

namespace spa.Utils
{
    internal static class ConfigHelper
    {
        public static IConfiguration _configuration;

        /// <summary>
        /// 这个是当前的程序启动路径
        /// </summary>
        public static string ContentRootPath;

        /// <summary>
        /// 这个是当前的程序启动的路径+ wwwroot
        /// </summary>
        public static string WebRootPath;

        /// <summary>
        /// 这个是用来保存上传部署的zip文件保存目录 用来快速回滚用的
        /// </summary>
        public static string BackupPath;

        /// <summary>
        /// project用的配置
        /// </summary>
        public static string DefaultAppSettingsFile = "_appsettings_.json";

        /// <summary>
        /// 本地的配置
        /// </summary>
        public static string DefaultGlobalAppSettingsFile = "appsettings.json";

        /// <summary>
        /// 用户登录账密配置
        /// </summary>
        public static string CasBinUserSettingsFile = "casbin_user.json";

        /// <summary>
        /// casbin 策略
        /// </summary>
        public static string CasBinPolicyFile = "casbin_policy.csv";

        /// <summary>
        /// 服务端js代码
        /// </summary>
        public static string ServerJsFile = "_server_.js";

        /// <summary>
        /// 新创建的时候的标准index.html
        /// </summary>
        public static string NewIndexHtmlTemplete = "<!DOCTYPE html>\n" +
            "<html lang=\"en\">\n" +
            "<head>\n" +
            "    <meta charset=\"UTF-8\">\n" +
            "    <title>@Title@</title>\n" +
            "</head>\n" +
            "<body>请重新上次部署\n" +
            "</body>\n" +
            "</html>";

        /// <summary>
        /// 获取配置
        /// 先从环境变量读取 不行再从appsettings.json文件里面
        /// </summary>
        /// <param name="key">xxx:yyy的格式</param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static string GetConfig(string key, string defaultValue = default)
        {
            var value = Environment.GetEnvironmentVariable(key.Replace(":", "_"));
            if (string.IsNullOrEmpty(value))
            {
                return _configuration[key];
            }

            return defaultValue;
        }


        /// <summary>
        /// 创建casbin
        /// </summary>
        /// <returns></returns>
        public static Enforcer createEnforcer()
        {
            var e = new Enforcer();
            var m = NetCasbin.Model.Model.CreateDefault();
            m.AddDef("r", "r", "sub, obj, act");
            m.AddDef("p", "p", "sub, obj, act");
            m.AddDef("e", "e", "some(where (p.eft == allow))");
            m.AddDef("m", "m", "r.sub == p.sub && keyMatch(r.obj, p.obj) && regexMatch(r.act, p.act)");
            var csv = Path.Combine(WebRootPath, CasBinPolicyFile);
            if (!File.Exists(csv))
            {
                File.CreateText(csv);
            }

            using var fs = new FileStream(csv, FileMode.Open, FileAccess.Read, FileShare.Read);
            IAdapter a = new DefaultFileAdapter(fs);
            e.SetModel(m);
            e.SetAdapter(a);
            e.LoadPolicy();
            return e;
        }

        /// <summary>
        /// 根据请求路径获取要请求的文件名称
        /// </summary>
        /// <param name="hrefLink"></param>
        /// <returns></returns>
        public static string GetFileNameByRequestPath(string hrefLink)
        {
            if (string.IsNullOrEmpty(hrefLink))
            {
                return string.Empty;
            }

            string[] parts = hrefLink.Split('/');
            string fileName = "";

            if (parts.Length > 0)
                fileName = parts[parts.Length - 1];
            else
                fileName = hrefLink;

            return fileName;
        }

        public static bool Auth(this Enforcer ob, string sub, string obj, string act)
        {
            try
            {
                return ob.Enforce(sub, obj, act);
            }
            catch (Exception _)
            {
                return false;
            }
        }
    }
}