using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using Dapper;
using MySqlConnector;
using Newtonsoft.Json;

namespace spa.Models
{

    public class DbContext
    {
        private readonly string _type;
        private readonly string _name;
        private readonly bool isSqlserver;

        public DbContext(string type, string name)
        {
            _type = type;
            isSqlserver = !type.Equals("mysql");
            _name = name;
        }


        /// <summary>
        /// 执行 insert update delete
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public int Excute(string sql, object param)
        {
            if (param != null && param is ExpandoObject properties)
            {
                var dbArgs = new DynamicParameters();
                foreach (var property in properties)
                {
                    dbArgs.Add(property.Key, property.Value);
                }

                if (dbArgs.ParameterNames != null && dbArgs.ParameterNames.Any())
                {
                    using (var connection = getConnection())
                    {
                        return connection.Execute(sql, dbArgs);
                    }

                }
            }

            using (var connection = getConnection())
            {
                return connection.Execute(sql);
            }
        }


        /// <summary>
        /// DB执行返回主键
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string ExecuteScalar(string sql, object param)
        {
            if (isSqlserver)
            {
                if (!sql.Contains("select scope_identity()"))
                {
                    sql += sql.EndsWith(";") ? "select scope_identity()" : ";select scope_identity()";
                }
            }
            else
            {
                if (!sql.Contains("select last_insert_id()"))
                {
                    sql += sql.EndsWith(";") ? "select last_insert_id()" : ";select last_insert_id()";
                }
            }

            if (param != null && param is ExpandoObject properties)
            {
                var dbArgs = new DynamicParameters();
                foreach (var property in properties)
                {
                    dbArgs.Add(property.Key, property.Value);
                }

                if (dbArgs.ParameterNames != null && dbArgs.ParameterNames.Any())
                {
                    using (var connection = getConnection())
                    {
                        return connection.ExecuteScalar<string>(sql, dbArgs);
                    }

                }
            }

            using (var connection = getConnection())
            {
                return connection.ExecuteScalar<string>(sql);
            }
        }


        /// <summary>
        /// DB执行返回一个DataTable
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public DataTable QueryTable(string sql, object param)
        {
            if (param != null && param is ExpandoObject properties)
            {
                var dbArgs = new DynamicParameters();
                foreach (var property in properties)
                {
                    dbArgs.Add(property.Key, property.Value);
                }

                if (dbArgs.ParameterNames != null && dbArgs.ParameterNames.Any())
                {
                    using (var connection = getConnection())
                    {
                        var dt = new DataTable();
                        dt.Load(connection.ExecuteReader(sql, dbArgs));
                        return dt;
                    }

                }

            }

            using (var connection = getConnection())
            {
                var dt = new DataTable();
                dt.Load(connection.ExecuteReader(sql));
                return dt;
            }
        }




        private DbConnection getConnection()
        {
            switch (_type)
            {
                case "mysql":
                    return new MySqlConnection(_name);
                case "mssql":
                case "sqlserver":
                    return new SqlConnection(_name);
                default:
                    return null;
            }
        }


    }

    public class JsDbContext
    {
        public DbContext DB;


        public JsDbContext(string type, string name)
        {
            DB = new DbContext(type, name);
        }


        /// <summary>
        /// 执行sql并拿到主键
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public string insertWithIdentity(string sql)
        {
            return insertWithIdentity(sql, null);
        }

        /// <summary>
        /// 执行sql并拿到主键
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string insertWithIdentity(string sql, object param)
        {
            return DB.ExecuteScalar(sql, param);

        }

        /// <summary>
        /// 封装成js方法
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string query(string sql, object param)
        {
            DataTable table = DB.QueryTable(sql, param);
            var JSONString = DataTableToJSONWithJavaScriptSerializer(table);
            return JSONString;
        }

        /// <summary>
        /// 在调用之前得注意sql注入
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public string query(string sql)
        {
            return query(sql, null);
        }

        /// <summary>
        /// 执行sql
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public int exec(string sql, object param)
        {
            return DB.Excute(sql, param);
        }

        /// <summary>
        /// 在调用之前得注意sql注入
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public int exec(string sql)
        {
            return exec(sql, null);
        }

        public static string DataTableToJSONWithJavaScriptSerializer(DataTable table)
        {
            Newtonsoft.Json.JsonSerializer json = new Newtonsoft.Json.JsonSerializer();

            json.NullValueHandling = NullValueHandling.Ignore;

            json.ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace;
            json.MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore;
            json.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            json.Converters.Add(new DataRowConverter());
            json.Converters.Add(new DataTableConverter());
            StringWriter sw = new StringWriter();
            Newtonsoft.Json.JsonTextWriter writer = new JsonTextWriter(sw);
            writer.Formatting = Formatting.None;
            writer.QuoteChar = '"';
            json.Serialize(writer, table);

            string output = sw.ToString();
            writer.Close();
            sw.Close();

            return output;
        }
    }


    /// <summary>
    /// Converts a <see cref="DataRow"/> object to and from JSON.
    /// </summary>
    public class DataRowConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DataRow row = value as DataRow;

            writer.WriteStartObject();
            foreach (DataColumn column in row.Table.Columns)
            {
                writer.WritePropertyName(column.ColumnName);
                serializer.Serialize(writer, row[column]);
            }

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines whether this instance can convert the specified value type.
        /// </summary>
        /// <param name="valueType">Type of the value.</param>
        /// <returns>
        ///     <c>true</c> if this instance can convert the specified value type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type valueType)
        {
            return typeof(DataRow).IsAssignableFrom(valueType);
        }
    }


    /// <summary>
    /// Converts a DataTable to JSON. Note no support for deserialization
    /// </summary>
    public class DataTableConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DataTable table = value as DataTable;
            DataRowConverter converter = new DataRowConverter();

            writer.WriteStartArray();

            foreach (DataRow row in table.Rows)
            {
                converter.WriteJson(writer, row, serializer);
            }

            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines whether this instance can convert the specified value type.
        /// </summary>
        /// <param name="valueType">Type of the value.</param>
        /// <returns>
        ///     <c>true</c> if this instance can convert the specified value type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type valueType)
        {
            return typeof(DataTable).IsAssignableFrom(valueType);
        }
    }
}