//-----------------------------------------------------------------------
// <copyright file="UploadFolderFile .cs" company="Company">
// Copyright (C) Company. All Rights Reserved.
// </copyright>
// <author>nainaigu</author>
// <create>$Date$</create>
// <summary></summary>
//-----------------------------------------------------------------------

using System.Data;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace spa.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// 上传文件夹要解析的每个文件
    /// </summary>
    public class FormFolder:IFormFile,IDisposable
    {
        private IFormFile[] _formFiles;
        private MemoryStream outStream;
        public FormFolder(string name,IFormFile[] formFiles)
        {
            _formFiles = formFiles;
            this.Name = name + ".zip";
            this.FileName = name + ".zip";
            this.ContentType = "application/x-zip-compressed";
            outStream = new MemoryStream();
           
            List<Tuple<string,bool,IFormFile>> findList  = new List<Tuple<string,bool,IFormFile>>();
            List<Tuple<string,bool,IFormFile>> folderList = new List<Tuple<string,bool,IFormFile>>();
            Dictionary<string,string> dic = new Dictionary<string, string>();
            foreach (var file in formFiles)
            {
                this.Length += file.Length;
                var fileArr = file.Name.Split('/');
                if (fileArr.Length == 2 && fileArr[1].EndsWith("index.html"))
                {
                    WithIndexHtmlFile = true;
                }
                for (int i = 0; i < fileArr.Length; i++)
                {
                    if (i == fileArr.Length - 1)
                    {
                        //这个也就是全路径了
                        if (dic.ContainsKey(file.Name))
                        {
                            continue;
                        }
                        dic.Add(file.Name, file.Name);
                        findList.Add(new Tuple<string, bool,IFormFile>(file.Name,false,file));
                    }
                    else
                    {
                        string foldPath = string.Join('/', fileArr.Take(i + 1).ToList());
                        if (dic.ContainsKey(foldPath))
                        {
                            continue;
                        }
                        dic.Add(foldPath, foldPath);
                        folderList.Add(new Tuple<string, bool,IFormFile>(foldPath,true,file));
                    }
                }
            }  
            folderList.AddRange(findList);
            var allFileLength = folderList.Count();
            using (ZipArchive destination = new ZipArchive(outStream, ZipArchiveMode.Create, true))
            {
                foreach (var enumerateFileSystemInfo in folderList)
                {
                    if (!enumerateFileSystemInfo.Item2)
                    {
                        //是文件
                        ZipArchiveEntry zipArchiveEntry = destination.CreateEntry(enumerateFileSystemInfo.Item1);
                        zipArchiveEntry.LastWriteTime = DateTimeOffset.Now;
                        using (Stream destination1 = zipArchiveEntry.Open())
                        {
                            enumerateFileSystemInfo.Item3.CopyToAsync(destination1).ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                    }
                    else
                    {
                        destination.CreateEntry(enumerateFileSystemInfo.Item1 + "/");
                    }
                }
            }
            
            outStream.Seek(0, SeekOrigin.Begin);
        }

        public bool WithIndexHtmlFile { get; set; }
        
        public void CopyTo(Stream target)
        {
            if(outStream == null) throw new NoNullAllowedException(nameof(outStream));
            outStream.CopyTo(target);
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = new CancellationToken())
        {
            if(outStream == null) throw new NoNullAllowedException(nameof(outStream));
            await outStream.CopyToAsync(target,cancellationToken);
        }

        public Stream OpenReadStream()
        {
            return outStream;
        }

        public string ContentDisposition { get; }
        public string ContentType { get; }
        public string FileName { get; }
        public IHeaderDictionary Headers { get; }
        public long Length { get; }
        public string Name { get; }

        public void Dispose()
        {
            outStream?.Dispose();
        }
    }
}