using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace spa.Plugins
{

    /// <summary>
    /// js运行时日志
    /// </summary>
    public class LoggerPlugin
    {
        private static readonly Logger logger;
        static LoggerPlugin()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        public void info(string msg)
        {
            logger.Info(msg);
        }
        public void warn(string msg)
        {
            logger.Warn(msg);
        }
        public void error(string msg)
        {
            logger.Error(msg);
        }
        public void debug(string msg)
        {
            logger.Debug(msg);
        }
    }
}