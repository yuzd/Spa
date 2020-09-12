using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using spa.Filter;
using spa.JavaScriptViewEngine.Utils;
using spa.Models;

namespace spa.Controller
{
    public class HomeController : Microsoft.AspNetCore.Mvc.Controller
    {
        
        public IActionResult Index()
        {
            return View("js-{auto}");
        }
        
        [Route("admin")]
        [BasciAuth]
        public IActionResult Admin()
        {
            if (string.IsNullOrEmpty(ConfigHelper.WebRootPath))
            {
                ViewBag.List = new List<SpaModel>();
                return View();
            }
            var folderList = Directory.GetDirectories(ConfigHelper.WebRootPath);
            var list = (from d in folderList
                    let f = new DirectoryInfo(d)
                    select new SpaModel
                    {
                        Title = f.Name,
                        DateTime = f.LastWriteTime,
                        Time = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Rollback = GetFirstBackup(f.Name)
                    }).Where(r => !r.Title.Equals("admin"))
                .OrderByDescending(r=>r.DateTime)
                .ToList();
            
            ViewBag.List = list;
            return View();
        }
        
        /// <summary>
        /// 获取前一个版本
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string GetFirstBackup(string path)
        {
            var resultList = string.Empty;
            if(!Directory.Exists(ConfigHelper.BackupPath)) return resultList;
            var backupFolder = Path.Combine(ConfigHelper.BackupPath, path);
            if (!Directory.Exists(backupFolder)) return resultList;
            var backupFiles = Directory.GetFiles(backupFolder)
                .Select(Path.GetFileNameWithoutExtension)
                .Select(r => new {Path = Path.Combine(backupFolder, r + ".zip"), Name = r, Time = DateTime.ParseExact(r.Split('_')[0], "yyyyMMddHHmmss", null), Size = long.Parse(r.Split('_')[1])})
                .OrderByDescending(r => r.Time)
                .Skip(1)//排除当前的
                .Select(t=>t.Name)
                .FirstOrDefault();
            return backupFiles;
        }
    }
}