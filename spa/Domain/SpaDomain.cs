using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using spa.Models;
using spa.Utils;

namespace spa.Domain;

public class SpaDomain
{
    private readonly RazorRenderEngine _engine;
    private static readonly Logger logger;
    private readonly string _backupFolder;
    private readonly int _backUpLimit = 1;
    public readonly Dictionary<string, MethodInfo> apiList;

    static SpaDomain()
    {
        logger = LogManager.GetCurrentClassLogger();
    }

    public SpaDomain(RazorRenderEngine engine)
    {
        _engine = engine;
        var backupLimit = ConfigHelper.GetConfig("BackUpLimit", "1");
        if (int.TryParse(backupLimit, out var backUpLimit))
        {
            _backUpLimit = backUpLimit;
        }

        _backupFolder = ConfigHelper.BackupPath;
        if (!Directory.Exists(_backupFolder)) Directory.CreateDirectory(_backupFolder);

        // 获取所有的api
        this.apiList = (from method in typeof(SpaDomain).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                let api = method.GetCustomAttribute<SpaApi>()
                where api != null
                select new
                {
                    Name = method.Name,
                    Method = method
                })
            .ToDictionary(r => r.Name, y => y.Method);
    }

    internal bool IsSpaApi(string api)
    {
        return this.apiList.ContainsKey(api);
    }

    internal MethodInfo getApi(string api)
    {
        this.apiList.TryGetValue(api, out var methodInfo);
        return methodInfo;
    }

    /// <summary>
    /// 获取Api列表
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    [SpaApi("获取Api列表", NotCheck = true)]
    private async Task getapis(string path, HttpContext context)
    {
        var apilist = getApiNames();
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(apilist));
    }

    private IEnumerable<SpaApiItem> getApiNames()
    {
        var apilist = from api in this.apiList
            let sp = api.Value.GetCustomAttribute<SpaApi>()
            where sp != null && !sp.NotCheck && sp.SupperRequired != "*"
            select new SpaApiItem
            {
                api = api.Key,
                name = sp.Name,
                url = "/" + api.Key
            };
        return apilist;
    }

    /// <summary>
    /// 获取用户列表 非supper
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    [SpaApi("获取用户列表", SupperRequired = "*")]
    private async Task getusers(string path, HttpContext context)
    {
        var jsonFile = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.CasBinUserSettingsFile);
        if (!File.Exists(jsonFile))
        {
            await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new List<String>()));
            return;
        }

        var userList = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<String, String>>(await File.ReadAllTextAsync(jsonFile));
        if (userList == null || !userList.Any())
        {
            await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new List<String>()));
            return;
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(userList.Keys.Select(r => new { name = r }).ToList()));
    }

    /// <summary>
    /// 新增用户 非supper
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    [SpaApi("新增用户", SupperRequired = "*")]
    private async Task addspauser(string path, HttpContext context)
    {
        using StreamReader reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(json))
        {
            await context.Response.WriteAsync($"err: data is empty!");
            return;
        }

        var user = Newtonsoft.Json.JsonConvert.DeserializeObject<SpaUser>(json);
        if (user == null || String.IsNullOrEmpty(user.LoginName) || String.IsNullOrEmpty(user.Pwd))
        {
            await context.Response.WriteAsync($"err: data is error!");
            return;
        }

        var jsonFile = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.CasBinUserSettingsFile);
        if (!File.Exists(jsonFile))
        {
            await File.WriteAllTextAsync(jsonFile, "{}");
        }

        var userList = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<String, String>>(await File.ReadAllTextAsync(jsonFile));
        if (userList != null)
        {
            userList[user.LoginName] = user.Pwd;
        }

        await File.WriteAllTextAsync(jsonFile, JsonConvert.SerializeObject(userList));
        await context.Response.WriteAsync("success");
    }

    /// <summary>
    /// 删除用户 非supper
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    [SpaApi("删除用户", SupperRequired = "*")]
    private async Task deleteuser(string path, HttpContext context)
    {
        var jsonFile = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.CasBinUserSettingsFile);
        if (!File.Exists(jsonFile))
        {
            await context.Response.WriteAsync("success");
            return;
        }

        var userList = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<String, String>>(await File.ReadAllTextAsync(jsonFile));
        if (userList == null)
        {
            await context.Response.WriteAsync("success");
            return;
        }

        if (!userList.ContainsKey(path))
        {
            await context.Response.WriteAsync("success");
            return;
        }

        userList.Remove(path);
        await File.WriteAllTextAsync(jsonFile, JsonConvert.SerializeObject(userList));
        await context.Response.WriteAsync("success");
    }

    /// <summary>
    /// 获取当前用户
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    [SpaApi("获取当前用户", NotCheck = true)]
    private async Task getuser(string path, HttpContext context)
    {
        var spaUser = context.Items["spaUser"] as SpaUser;
        if (spaUser == null)
        {
            spaUser = new SpaUser
            {
                LoginName = "none"
            };
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(spaUser));
    }

    /// <summary>
    /// 新创建只有supper有权限
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    [SpaApi("创建新项目", SupperRequired = "*")]
    private async Task create(string path, HttpContext context)
    {
        await upload(path, context, false);
    }

    /// <summary>
    /// 重新部署
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    [SpaApi("部署")]
    private async Task reupload(string path, HttpContext context)
    {
        await upload(path, context, true);
    }

    /// <summary>
    /// 回滚到指定版本
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [SpaApi("回滚")]
    private async Task rollback(string path, HttpContext context)
    {
        using StreamReader reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        if (String.IsNullOrEmpty(json))
        {
            await context.Response.WriteAsync("path is invalid");
            return;
        }
        var arr = json.Split('-');
        if (arr.Length != 2)
        {
            await context.Response.WriteAsync("path is invalid");
            return;
        }

        var action = path;
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

        Action<string> saveAction = temp => { File.Copy(fileName, temp); };

        await Deploy(context, destFolder, saveAction, () => { });
    }

    /// <summary>
    /// casbin保存
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [SpaApi("保存权限配置", SupperRequired = "*")]
    private async Task savepolicy(string path, HttpContext context)
    {
        using StreamReader reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(json))
        {
            await context.Response.WriteAsync($"err: json is empty!");
            return;
        }

        var filePath = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.CasBinPolicyFile);
        if (!File.Exists(filePath))
        {
            File.CreateText(filePath);
            return;
        }

        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<String>>(json);
        await File.WriteAllTextAsync(filePath, String.Join(Environment.NewLine, list!));
        await context.Response.WriteAsync("success");
    }

    /// <summary>
    /// casbin获取
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [SpaApi("获取权限配置", SupperRequired = "*")]
    private async Task getpolicy(string path, HttpContext context)
    {
        var filePath = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.CasBinPolicyFile);
        if (!File.Exists(filePath))
        {
            File.CreateText(filePath);
            return;
        }

        var data = await File.ReadAllLinesAsync(filePath);
        var list = data.Where(r => !string.IsNullOrEmpty(r) && r.Length > 5);
        await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(list));
    }

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="path"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [SpaApi("删除项目", SupperRequired = "*")]
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
    /// <param name="project"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [SpaApi("获取列表", NotCheck = true)]
    private Task pathlist(string project, HttpContext context)
    {
        var apilist = getApiNames();
        var spaUser = context.Items["spaUser"] as SpaUser;
        // e.Enforce(sub, obj, act)
        var e = ConfigHelper.createEnforcer();
        var folderList = Directory.GetDirectories(ConfigHelper.WebRootPath);
        var list = (from d in folderList
                let f = new DirectoryInfo(d)
                let auth = spaUser!.Supper || (apilist.Any(y => e.Auth(spaUser.LoginName, f.Name, y.url)))
                let last = GetFirstBackup(f.Name)
                where f.Name != "admin" && f.Name != "_backup_" && auth
                select new SpaModel
                {
                    Title = f.Name,
                    DateTime = f.LastWriteTime,
                    Time = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Rollback = last.Item2.Item1,
                    User = last.Item1.Item2
                }
            )
            .ToList();
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(list));
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <param name="project"></param>
    /// <param name="context"></param>
    [SpaApi("获取配置", SupperRequired = "global")]
    private async Task getconfigjson(string project, HttpContext context)
    {
        string jsonFile = "";
        if (project == "global")
        {
            jsonFile = Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.DefaultAppSettingsFile);
        }
        else
        {
            jsonFile = Path.Combine(ConfigHelper.WebRootPath, project, ConfigHelper.DefaultAppSettingsFile);
        }

        if (!File.Exists(jsonFile))
        {
            await File.WriteAllTextAsync(jsonFile, "{}");
            return;
        }

        await context.Response.WriteAsync($"{await File.ReadAllTextAsync(jsonFile)}");
    }

    /// <summary>
    /// 获取server.js
    /// </summary>
    /// <param name="project"></param>
    /// <param name="context"></param>
    [SpaApi("服务端脚本获取")]
    private async Task serverjsget(string project, HttpContext context)
    {
        var jsonFile = Path.Combine(ConfigHelper.WebRootPath, project, ConfigHelper.ServerJsFile);
        if (!File.Exists(jsonFile))
        {
            await context.Response.WriteAsync($"notfound");
            return;
        }

        await context.Response.WriteAsync($"{await File.ReadAllTextAsync(jsonFile)}");
        return;
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="project"></param>
    /// <param name="context"></param>
    [SpaApi("保存配置", SupperRequired = "global")]
    private async Task saveconfigjson(string project, HttpContext context)
    {
        using StreamReader reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(json))
        {
            await context.Response.WriteAsync($"err: json is empty!");
            return;
        }

        var currentPathType = project;
        var jsonString = ConvertJsonString(json);
        if (string.IsNullOrEmpty(jsonString))
        {
            await context.Response.WriteAsync($"err: invaild json !");
            return;
        }

        var jsonFile = currentPathType == "global"
            ? Path.Combine(ConfigHelper.WebRootPath, ConfigHelper.DefaultAppSettingsFile)
            : Path.Combine(ConfigHelper.WebRootPath, currentPathType, ConfigHelper.DefaultAppSettingsFile);
        await File.WriteAllTextAsync(jsonFile, jsonString);
        await context.Response.WriteAsync($"success");
        return;
    }

    /// <summary>
    /// 保存server.js
    /// </summary>
    /// <param name="spa"></param>
    /// <param name="context"></param>
    [SpaApi("服务端脚本保存")]
    private async Task serverjssave(string spa, HttpContext context)
    {
        using StreamReader reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(json))
        {
            await context.Response.WriteAsync($"err: js is empty!");
            return;
        }

        var jsonFile = Path.Combine(ConfigHelper.WebRootPath, spa, ConfigHelper.ServerJsFile);
        await File.WriteAllTextAsync(jsonFile, json);
        await context.Response.WriteAsync($"success");
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

        //保存zip
        if (!Directory.Exists(filePath))
        {
            //创建文件夹
            Directory.CreateDirectory(filePath);
        }

        var files = context.Request.Form.Files;
        if (files.Count < 1)
        {
            await File.WriteAllTextAsync(Path.Combine(filePath, "index.html"), ConfigHelper.NewIndexHtmlTemplete.Replace("@Title@", path));
            await context.Response.WriteAsync("success");
            
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

        //保存文件流到文件
        void SaveFile(string temp)
        {
            using var inputStream = new FileStream(temp, FileMode.Create);
            file.CopyToAsync(inputStream).ConfigureAwait(false).GetAwaiter().GetResult();
            byte[] array = new byte[inputStream.Length];
            inputStream.Seek(0, SeekOrigin.Begin);
            inputStream.Read(array, 0, array.Length);
        }

        //备份
        void BackupAction()
        {
            async void Action()
            {
                await _engine.RenderCache(path);
            }

            new Task(Action).Start();
            var spaUser = context.Items["spaUser"] as SpaUser;
            BackupUpload(path, DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + spaUser!.LoginName, Path.Combine(filePath, "_new_.zip"));
            (file as FormFolder)?.Dispose(); //文件夹上传
        }

        await Deploy(context, filePath, SaveFile, BackupAction);
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
                UserName = r.Split('_')[1]
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
    /// 是否是正确的文件夹名称
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private bool IsValidPath(string path)
    {
        return Path.GetInvalidFileNameChars().Count(x => path.Contains((char)x)) < 1 && !path.ToLower().Equals("admin") &&
               !path.ToLower().Equals("_back_up") && !path.ToLower().Equals("global");
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
    private ((string, string),(string, string)) GetFirstBackup(string path)
    {
        var resultList = ((string.Empty, String.Empty),(string.Empty, String.Empty));
        if (!Directory.Exists(ConfigHelper.BackupPath)) return resultList;
        var backupFolder = Path.Combine(ConfigHelper.BackupPath, path);
        if (!Directory.Exists(backupFolder)) return resultList;
        var backupFiles = Directory.GetFiles(backupFolder)
            .Select(Path.GetFileNameWithoutExtension)
            .Select(r => new
            {
                Path = Path.Combine(backupFolder, r + ".zip"),
                Name = r,
                Time = DateTime.ParseExact(r.Split('_')[0], "yyyyMMddHHmmss", null),
                UserName = r.Split('_')[1]
            })
            .OrderByDescending(r => r.Time).ToList();

        var lastFile = backupFiles.Select(t => new Tuple<String,String>(t.Name,t.UserName))
            .FirstOrDefault();
        if (lastFile == null)
        {
            return ((string.Empty, String.Empty),(string.Empty, String.Empty));
        }
        
        var rollFile = backupFiles
            .Skip(1) //排除当前的
            .Select(t => new Tuple<String,String>(t.Name,t.UserName))
            .FirstOrDefault();
        if (rollFile == null)
        {
            return ((lastFile.Item1,lastFile.Item2 ),("",""));
        }
        return ((lastFile.Item1,lastFile.Item2 ),(rollFile.Item1,rollFile.Item2));
    }

    private List<string> GetBackupList(string path)
    {
        var resultList = new List<string>();
        var backupFolder = Path.Combine(_backupFolder, path);
        if (!Directory.Exists(backupFolder)) return resultList;
        var backupFiles = Directory.GetFiles(backupFolder)
            .Select(Path.GetFileNameWithoutExtension)
            .Select(r => new
            {
                Path = Path.Combine(backupFolder, r + ".zip"), Name = r + ".zip", Time = DateTime.ParseExact(r.Split('_')[0], "yyyyMMddHHmmss", null),
                UserName = r.Split('_')[1]
            })
            .OrderByDescending(r => r.Time)
            .Select(Newtonsoft.Json.JsonConvert.SerializeObject)
            .ToList();
        return backupFiles;
    }
}