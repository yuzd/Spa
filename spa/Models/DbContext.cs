using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using AntData.ORM.Data;
using AntData.ORM.Linq;
using Newtonsoft.Json;

namespace spa.Models
{
    public class DbContext
    {
        public static MysqlDbContext<LitoEntitys> DB => new MysqlDbContext<LitoEntitys>("pro");

        /// <summary>
        /// 封装成js方法
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public static string Query(string sql, object param)
        {
            DataTable table = null;
            if (param != null && param is ExpandoObject properties)
            {
                List<DataParameter> dataParameters = new List<DataParameter>();
                foreach (var property in properties)
                {
                    dataParameters.Add(new DataParameter(property.Key, property.Value));
                }

                if (dataParameters.Any()) table = DB.QueryTable(sql, dataParameters.ToArray());
            }

            if (table == null)
            {
                table = DB.QueryTable(sql);
            }

            var JSONString = DataTableToJSONWithJavaScriptSerializer(table);
            return JSONString;
        }

        /// <summary>
        /// 在调用之前得注意sql注入
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public static string Query(string sql)
        {
            DataTable table = DB.QueryTable(sql);
            var JSONString = DataTableToJSONWithJavaScriptSerializer(table);
            return JSONString;
        }

        /// <summary>
        /// 执行sql
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public static bool Excute(string sql, object param)
        {
            if (param != null && param is ExpandoObject properties)
            {
                List<DataParameter> dataParameters = new List<DataParameter>();
                foreach (var property in properties)
                {
                    dataParameters.Add(new DataParameter(property.Key, property.Value));
                }

                if (dataParameters.Any())
                {
                    return DB.Execute<int>(sql, dataParameters.ToArray()) > 0;
                }
            }

            return DB.Execute<int>(sql) > 0;
        }

        /// <summary>
        /// 在调用之前得注意sql注入
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public static bool Excute(string sql)
        {
            var result = DB.Execute<int>(sql);
            return result > 0;
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

    public partial class LitoEntitys : IEntity
    {
        public IQueryable<T> Get<T>() where T : class
        {
            throw new NotImplementedException();
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