using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using JavaScriptViewEngine.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using spa.JavaScriptViewEngine.Utils;
using spa.Models;

namespace spa.Controller
{
    /// <summary>
    /// api
    /// </summary>
    public class ApiMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Logger logger;
        private readonly string _backupFolder;
        private readonly int _backUpLimit = 1;

        static ApiMiddleware()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        public ApiMiddleware(RequestDelegate next)
        {
            _next = next;

            var backupLimit = ConfigHelper.GetConfig("BackUpLimit", "1");
            if (int.TryParse(backupLimit, out var backUpLimit))
            {
                _backUpLimit = backUpLimit;
            }

            _backupFolder = ConfigHelper.BackupPath;
            if (!Directory.Exists(_backupFolder)) Directory.CreateDirectory(_backupFolder);
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                if (!AuthCheck(context)) return;

                var path = context.Request.Path.Value.ToLower();
                var action = path.Split('/').LastOrDefault();
                if (string.IsNullOrEmpty(action))
                {
                    await context.Response.WriteAsync("action is null");
                    return;
                }

                action = action.ToLower();
                //获取单页面应用列表
                if (action.EndsWith(".pathlist"))
                {
                    await list(context);
                    return;
                }
                //回滚某个单页面应用到指定版本
                else if (action.EndsWith(".rollback"))
                {
                    await rollback(action.Replace(".rollback", ""), context);
                    return;
                }
                //新创建并上传一个单页面应用
                else if (action.EndsWith(".upload"))
                {
                    await upload(action.Replace(".upload", ""), context);
                    return;
                }
                //对已上传的单页面应用更新
                else if (action.EndsWith(".reupload"))
                {
                    await upload(action.Replace(".reupload", ""), context, true);
                    return;
                }
                //删除一个单页面应用
                else if (action.EndsWith(".delete"))
                {
                    await delete(action.Replace(".delete", ""), context);
                    return;
                }
                else if (action.EndsWith(".api"))
                {
                    await api(action.Replace(".api", ""), context);
                    return;
                }

                await context.Response.WriteAsync("invaild api");
            }
            catch (Exception e)
            {
                logger.Error(e);
                await context.Response.WriteAsync(e.ToString());
            }
        }



        /// <summary>
        /// 验证auth
        /// </summary>
        /// <param name="context"></param>
        /// <param name="isHtml"></param>
        /// <returns></returns>
        internal static bool AuthCheck(HttpContext context,bool isHtml = false)
        {
            if (!context.Request.Headers.ContainsKey(nameof(Authorization)))
            {
                context.Response.StatusCode = 401;
                if (isHtml)
                {
                    context.Response.Headers.Add("WWW-Authenticate", (StringValues)"BASIC realm=\"api\"");
                    context.Response.StatusCode = 401;
                }
                return false;
            }

            var authHeader = AuthenticationHeaderValue.Parse(context.Request.Headers[nameof(Authorization)]);
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
            var username = credentials[0];
            var password = credentials[1];
            var localUserName = ConfigHelper.GetConfig("BasicAuth:Name");
            var localPassword = ConfigHelper.GetConfig("BasicAuth:Pwd");
            if (!string.IsNullOrEmpty(localUserName) && !string.IsNullOrEmpty(localPassword) && (!username.Equals(localUserName) ||
                                                                                                 !password.Equals(localPassword)))
            {
                context.Response.StatusCode = 401;
                if (isHtml)
                {
                    context.Response.Headers.Add("WWW-Authenticate", (StringValues)"BASIC realm=\"api\"");
                    context.Response.StatusCode = 401;
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// 上传
        /// </summary>
        /// <param name="path"></param>
        /// <param name="context"></param>
        /// <param name="isReUpload">是否是重新上传</param>
        /// <returns></returns>
        private async Task upload(string path, HttpContext context, bool isReUpload = false)
        {
            if (!IsValidPath(path))
            {
                await context.Response.WriteAsync("invaild path");
                return;
            }

            //根目录
            var filePath = Path.Combine(ConfigHelper.WebRootPath, path);
            if (!isReUpload)
            {
                //查看是否已经存在
                if (Directory.Exists(filePath))
                {
                    await context.Response.WriteAsync($"{path} is existed!");
                    return;
                }
            }

            var files = context.Request.Form.Files;
            if (files.Count < 1)
            {
                await context.Response.WriteAsync("invaild file");
                return;
            }
            var file = files.First();
            if (files.Count > 1)
            {
                //文件夹上传
                //检查根目录是否含有index.html文件
                var folderFile = new FormFolder(path, files.ToArray());
                if (!folderFile.WithIndexHtmlFile)
                {
                    await context.Response.WriteAsync("not found index.html file");
                    return;
                }

                file = folderFile;
            }

            if (file.Length < 1 || (!string.IsNullOrEmpty(file.Name) && !file.Name.ToLower().EndsWith(".zip")))
            {
                await context.Response.WriteAsync("invaild file");
                return;
            }

            //保存zip
            if (!Directory.Exists(filePath))
            {
                //创建文件夹
                Directory.CreateDirectory(filePath);
            }


            //保存文件流到文件
            Action<string> saveFile = (temp) =>
           {
               using (var inputStream = new FileStream(temp, FileMode.Create))
               {
                   file.CopyToAsync(inputStream).ConfigureAwait(false).GetAwaiter().GetResult();
                   byte[] array = new byte[inputStream.Length];
                   inputStream.Seek(0, SeekOrigin.Begin);
                   inputStream.Read(array, 0, array.Length);
               }
           };

            //备份
            Action backupAction = () =>
            {
                BackupUpload(path, DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + file.Length, Path.Combine(filePath, "_new_.zip"));
                (file as FormFolder)?.Dispose();//文件夹上传
            };

            await Deploy(context, filePath, saveFile, backupAction);
        }

        /// <summary>
        /// 部署
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filePath">所属单页面的根目录文件夹</param>
        /// <param name="saveFile">保存到单页面根目录文件夹下的zip的委托</param>
        /// <param name="backupAction">备份委托</param>
        /// <returns></returns>
        private static async Task Deploy(HttpContext context, string filePath, Action<string> saveFile, Action backupAction)
        {
            //保存到的目的文件
            var guidFile = Path.Combine(filePath, "_new_.zip");

            //解压后的文件夹
            var destFolder = Path.Combine(filePath, "_new_");

            try
            {
                //看是否有人在处理了？
                if (File.Exists(guidFile))
                {
                    //查看是否创建了超过1分钟了
                    if ((DateTime.Now - new FileInfo(guidFile).CreationTime).TotalMinutes > 1)
                    {
                        File.Delete(guidFile);
                    }
                    else
                    {
                        //说明有人正在处理
                        await context.Response.WriteAsync("please wait 5 sencods and retry");
                        return;
                    }
                }

                //真是处理前就先删除
                if (Directory.Exists(destFolder)) Directory.Delete(destFolder, true);

                //保存到guidFile文件
                saveFile(guidFile);

                //解压zip
                ZipFile.ExtractToDirectory(guidFile, destFolder);

                var copyFromFolder = destFolder;
                var indexFile = Path.Combine(destFolder, "index.html");

                //查看是否有index.html
                if (!File.Exists(indexFile))
                {
                    //可能是有二级目录 找到哪个里面存在
                    var subFolders = Directory.GetFiles(destFolder, "index.html", SearchOption.AllDirectories);
                    if (subFolders.Length == 1)
                    {
                        // ReSharper disable once PossibleNullReferenceException
                        copyFromFolder = new FileInfo(subFolders.First()).Directory.FullName;
                    }
                    else
                    {
                        await context.Response.WriteAsync("can not found index.html in zip file");
                        return;
                    }
                }

                //替换文件
                CopyHelper.DirectoryCopy(copyFromFolder, filePath, true);

                //备份为了能快速回滚
                backupAction();

                await context.Response.WriteAsync("success");
            }
            finally
            {
                try
                {
                    File.Delete(guidFile);
                }
                catch (Exception)
                {
                    //ignore
                }
                try
                {
                    Directory.Delete(destFolder, true);
                }
                catch (Exception)
                {
                    //ignore
                }

            }
        }


        /// <summary>
        /// 回滚到指定版本
        /// </summary>
        /// <param name="path"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task rollback(string path, HttpContext context)
        {
            var arr = path.Split('-');
            if (arr.Length != 2)
            {
                await context.Response.WriteAsync("path is invalid");
                return;
            }

            var action = arr[0];
            var fileName = Path.Combine(_backupFolder, action, arr[1] + ".zip");
            if (!File.Exists(fileName))
            {
                await context.Response.WriteAsync($"{fileName} not exist");
                return;
            }

            var destFolder = Path.Combine(ConfigHelper.WebRootPath, action);
            if (!Directory.Exists(destFolder))
            {
                await context.Response.WriteAsync($"{destFolder} not exist");
                return;
            }

            Action<string> saveAction = temp =>
            {
                File.Copy(fileName, temp);
            };

            await Deploy(context, destFolder, saveAction, () => { });
        }

        /// <summary>
        /// 备份
        /// </summary>
        /// <param name="path">当前节点路径</param>
        /// <param name="now">批次号</param>
        /// <param name="guidFile">上传的源文件</param>
        private void BackupUpload(string path, string now, string guidFile)
        {
            //复制zip到backup文件夹
            var backupFolder = Path.Combine(_backupFolder, path);
            if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);
            var destZip = Path.Combine(backupFolder, now + ".zip");
            File.Copy(guidFile, destZip);

            //查看backup目录
            var backupFiles = Directory.GetFiles(backupFolder)
                .Where(r => !r.Equals(destZip))
                .Select(Path.GetFileNameWithoutExtension)
                .Select(r => new
                {
                    Path = Path.Combine(backupFolder, r + ".zip"),
                    Time = DateTime.ParseExact(r.Split('_')[0], "yyyyMMddHHmmss", null),
                    Size = long.Parse(r.Split('_')[1])
                })
                .OrderByDescending(r => r.Time)
                .ToList();

            //是否已经超过最大备份保存数
            if (backupFiles.Count <= _backUpLimit) return;

            //保留最新的 删除旧的
            foreach (var backupFile in backupFiles.Skip(_backUpLimit))
            {
                File.Delete(backupFile.Path);
            }
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="path"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task delete(string path, HttpContext context)
        {
            if (!IsValidPath(path))
            {
                await context.Response.WriteAsync("invaild path");
                return;
            }

            var filePath = Path.Combine(ConfigHelper.WebRootPath, path);
            if (!Directory.Exists(filePath))
            {
                await context.Response.WriteAsync("success");
                return;
            }

            var guidFile = Path.Combine(filePath, "_new_.zip");
            if (File.Exists(guidFile))
            {
                await context.Response.WriteAsync("please wait 5 sencods and retry");
                return;
            }

            Directory.Delete(filePath, true);
            //也得删除备份的文件夹
            var backupFolder = Path.Combine(_backupFolder, path);
            if (Directory.Exists(backupFolder)) Directory.Delete(backupFolder, true);
            await context.Response.WriteAsync("success");
        }

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private Task list(HttpContext context)
        {
            var folderList = Directory.GetDirectories(ConfigHelper.WebRootPath);
            var list = (from d in folderList
                        let f = new DirectoryInfo(d)
                        where f.Name != "admin" && f.Name != "_backup_"
                        select new SpaModel
                        {
                            Title = f.Name,
                            DateTime = f.LastWriteTime,
                            Time = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            Rollback = GetFirstBackup(f.Name)
                        }
                        )
                .ToList();
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(list));
        }

        private async Task api(string path, HttpContext context)
        {
            if (path.Equals("getconfigjson"))
            {
                string currentConfigType = "";
                using (StreamReader reader = new StreamReader(context.Request.Body))
                {
                    currentConfigType = await reader.ReadToEndAsync();
                }

                string jsonFile = "";
                if (currentConfigType == "global")
                {
                    jsonFile = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.DefaultAppSettingsFile);
                }
                else
                {
                    jsonFile = Path.Combine(ConfigHelper.WebRootPath, currentConfigType, ConfigHelper.DefaultAppSettingsFile);
                }
                if (!File.Exists(jsonFile))
                {
                    await File.WriteAllTextAsync(jsonFile, "{}");
                    return;
                }
                await context.Response.WriteAsync($"{await File.ReadAllTextAsync(jsonFile)}");
                return;
            }

            if (path.EndsWith("serverjsget"))
            {
                var spa = path.Split('-')[0];
                var jsonFile = Path.Combine(ConfigHelper.WebRootPath, spa, "_server_.js");
                if (!File.Exists(jsonFile))
                {
                    await context.Response.WriteAsync($"notfound");
                    return;
                }

                await context.Response.WriteAsync($"{await File.ReadAllTextAsync(jsonFile)}");
                return;
            }


            if (path.Equals("saveconfigjson"))
            {
                using (StreamReader reader = new StreamReader(context.Request.Body))
                {
                    var json = reader.ReadToEnd();
                    if (string.IsNullOrEmpty(json))
                    {
                        await context.Response.WriteAsync($"err: json is empty!");
                        return;
                    }

                    JToken jsonObj = JToken.Parse(json);
                    var currentPathType = jsonObj["type"]?.ToString() ?? "global";
                    var jsonString = ConvertJsonString(jsonObj["json"].ToString());
                    if (string.IsNullOrEmpty(jsonString))
                    {
                        await context.Response.WriteAsync($"err: invaild json !");
                        return;
                    }

                    var jsonFile = currentPathType == "global" ? Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.DefaultAppSettingsFile) : Path.Combine(ConfigHelper.WebRootPath, currentPathType, ConfigHelper.DefaultAppSettingsFile);
                    await File.WriteAllTextAsync(jsonFile, jsonString);
                    await context.Response.WriteAsync($"success");
                    return;
                }

            }

            if (path.EndsWith("serverjssave"))
            {
                var spa = path.Split('-')[0];

                using (StreamReader reader = new StreamReader(context.Request.Body))
                {
                    var json = reader.ReadToEnd();
                    if (string.IsNullOrEmpty(json))
                    {
                        await context.Response.WriteAsync($"err: js is empty!");
                        return;
                    }

                    var jsonFile = Path.Combine(ConfigHelper.WebRootPath, spa, "_server_.js");
                    File.WriteAllText(jsonFile, json);

                    await context.Response.WriteAsync($"success");
                    return;
                }
            }

        }

        /// <summary>
        /// 是否是正确的文件夹名称
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private bool IsValidPath(string path)
        {
            return Path.GetInvalidFileNameChars().Count(x => path.Contains((char)x)) < 1 && !path.ToLower().Equals("admin");
        }

        /// <summary>
        /// 序列化jsonstring
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string ConvertJsonString(string str, object obj1 = null)
        {
            try
            {
                //格式化json字符串
                JsonSerializer serializer = new JsonSerializer();
                TextReader tr = new StringReader(str);
                JsonTextReader jtr = new JsonTextReader(tr);
                object obj = obj1 ?? serializer.Deserialize(jtr);
                if (obj != null)
                {
                    StringWriter textWriter = new StringWriter();
                    JsonTextWriter jsonWriter = new JsonTextWriter(textWriter)
                    {
                        Formatting = Formatting.Indented,
                        Indentation = 4,
                        IndentChar = ' '
                    };
                    serializer.Serialize(jsonWriter, obj);
                    return textWriter.ToString();
                }
                else
                {
                    return str;
                }
            }
            catch (Exception e)
            {
                logger.Warn(e);
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取前一个版本
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string GetFirstBackup(string path)
        {
            var resultList = string.Empty;
            if (!Directory.Exists(ConfigHelper.BackupPath)) return resultList;
            var backupFolder = Path.Combine(ConfigHelper.BackupPath, path);
            if (!Directory.Exists(backupFolder)) return resultList;
            var backupFiles = Directory.GetFiles(backupFolder)
                .Select(Path.GetFileNameWithoutExtension)
                .Select(r => new { Path = Path.Combine(backupFolder, r + ".zip"), Name = r, Time = DateTime.ParseExact(r.Split('_')[0], "yyyyMMddHHmmss", null), Size = long.Parse(r.Split('_')[1]) })
                .OrderByDescending(r => r.Time)
                .Skip(1)//排除当前的
                .Select(t => t.Name)
                .FirstOrDefault();
            return backupFiles;
        }

        private List<string> GetBackupList(string path)
        {
            var resultList = new List<string>();
            var backupFolder = Path.Combine(_backupFolder, path);
            if (!Directory.Exists(backupFolder)) return resultList;
            var backupFiles = Directory.GetFiles(backupFolder)
                .Select(Path.GetFileNameWithoutExtension)
                .Select(r => new { Path = Path.Combine(backupFolder, r + ".zip"), Name = r + ".zip", Time = DateTime.ParseExact(r.Split('_')[0], "yyyyMMddHHmmss", null), Size = long.Parse(r.Split('_')[1]) })
                .OrderByDescending(r => r.Time)
                .Select(Newtonsoft.Json.JsonConvert.SerializeObject)
                .ToList();
            return backupFiles;
        }
    }
}