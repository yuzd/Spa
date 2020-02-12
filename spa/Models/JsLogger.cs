using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace spa.Models
{
   
    /// <summary>
    /// js运行时日志
    /// </summary>
    public class JsLogger
    {
        private static readonly Logger logger;
        static JsLogger()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        public static void Info(string msg)
        {
            logger.Info(msg);
        }
        public static void Warn(string msg)
        {
            logger.Warn(msg);
        }
        public static void Error(string msg)
        {
            logger.Error(msg);
        }
        public static void Debug(string msg)
        {
            logger.Debug(msg);
        }
    }
}