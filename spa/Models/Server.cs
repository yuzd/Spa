using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace spa.Models
{
    /// <summary>
    /// js调用入口
    /// </summary>
    public class Server
    {

        /// <summary>
        /// 创建组件
        /// </summary>
        /// <param name="type">组件类型</param>
        /// <param name="param">组件需要的参数</param>
        /// <returns></returns>
        public static object create(string type, object param)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new  ArgumentException("server.create(type, properties)->type invaild");
            }

            type = type.ToLower();

            if (type == "sql")
            {
                return CreateSql(param);
            }
            else if (type == "log")
            {
                return CreateLogger(param);
            }
            else if (type == "redis")
            {
                return CreateRedis(param);
            }
            else if (type == "http")
            {
                return CreateHttp(param);
            }

            throw new ArgumentException("server.create(type, properties)->type invaild");
        }

        public static object create(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("server.create(type, properties)->type invaild");
            }

            type = type.ToLower();

            if (type == "sql")
            {
                return CreateSql(null);
            }
            else if (type == "log")
            {
                return CreateLogger(null);
            }
            else if (type == "redis")
            {
                return CreateRedis(null);
            }
            else if (type == "http")
            {
                return CreateHttp(null);
            }

            throw new ArgumentException("server.create(type, properties)->type invaild");
        }
        /// <summary>
        /// 创建Redis组件
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        private static object CreateHttp(object param)
        {
            return new HttpContext(param);
        }

        /// <summary>
        /// 创建Redis组件
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        private static object CreateRedis(object param)
        {
            if (param == null || !(param is ExpandoObject properties))
            {
                throw new ArgumentException("server.create(type, properties)->properties invaild");
            }
            string name = "";
            foreach (var property in properties)
            {
                if (property.Key.ToLower().Equals("db"))
                {
                    name = property.Value.ToString();
                }
            }
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("server.create(type, properties)->properties.name invaild");
            }
            return new RedisClient(name);
        }


        /// <summary>
        /// 创建Logger组件
        /// </summary>
        /// <returns></returns>
        private static object CreateLogger(object param)
        {
            return new JsLogger();
        }

        /// <summary>
        /// 创建server组件
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        private static object CreateSql(object param)
        {
            if (param == null || !(param is ExpandoObject properties))
            {
                throw new ArgumentException("server.create(type, properties)->properties invaild");
            }

            string type = "mysql";
            string name = "";
            foreach (var property in properties)
            {
                if (property.Key.ToLower().Equals("type"))
                {
                    type = property.Value.ToString().ToLower();
                }
                else if (property.Key.ToLower().Equals("db"))
                {
                    name = property.Value.ToString();
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("server.create(type, properties)->properties.name invaild");
            }

            return new JsDbContext(type, name);
        }
    }
}
