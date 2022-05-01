using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using spa.Utils;

namespace spa.Models
{
    public class FileModel
    {
        public FileModel(IWebHostEnvironment env, string action, string file = "index.html")
        {
            Action = action;

            FilePath = env.WebRootPath
                       + Path.DirectorySeparatorChar.ToString()
                       + action
                       + Path.DirectorySeparatorChar.ToString()
                       + file;

            FileInfo = new FileInfo(FilePath);
        }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 文件名称
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// 是否存在
        /// </summary>
        public bool IsExist => File.Exists(FilePath);

        /// <summary>
        /// 文件的内容
        /// </summary>
        public string GetContent()
        {
            if (FileInfo != null)
            {
                Content = CopyHelper.ReadAllText(FilePath);
                return Content;
            }

            return string.Empty;
        }

        public string Content { get; set; }

        /// <summary>
        /// 文件对象
        /// </summary>
        public FileInfo FileInfo { get; set; }

        /// <summary>
        /// 文件的最后更新时间
        /// </summary>
        public DateTime LastModifyTime => new FileInfo(FilePath).LastWriteTime;
    }
}