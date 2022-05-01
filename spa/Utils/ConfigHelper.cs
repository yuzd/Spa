using System;
using Microsoft.Extensions.Configuration;

namespace spa.Utils
{
    internal class ConfigHelper
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
        /// 获取配置
        /// 先从环境变量读取 不行再从appsettings.json文件里面
        /// </summary>
        /// <param name="key">xxx:yyy的格式</param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static string GetConfig(string key,string defaultValue = default) 
        {
            var value = Environment.GetEnvironmentVariable(key.Replace(":","_"));
            if (string.IsNullOrEmpty(value))
            {
                return _configuration[key];
            }

            return defaultValue;
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
    }
}
