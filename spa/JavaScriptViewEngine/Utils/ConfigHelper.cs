using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace spa.JavaScriptViewEngine.Utils
{
    public class ConfigHelper
    {
        public static IConfiguration _configuration;

        public static string ContentRootPath;
        public static string WebRootPath;
        public static string BackupPath;

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
    }
}
