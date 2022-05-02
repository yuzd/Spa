using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace spa.Asset
{
    public class Asset:IDisposable
    {
        public Asset(Stream stream, string mediaType)
        {
            Stream = stream;
            MediaType = mediaType;
        }

        public Stream Stream { get; private set; }

        public string MediaType { get; private set; }

        public void Dispose()
        {
            Stream?.Dispose();
        }
    }


    public class EmbeddedAssetProvider
    {
        private readonly IDictionary<string, EmbeddedAssetDescriptor> _pathToAssetMap;

        public EmbeddedAssetProvider(
            IDictionary<string, EmbeddedAssetDescriptor> pathToAssetMap)
        {
            _pathToAssetMap = pathToAssetMap;
        }

        public Asset GetAsset(string path)
        {
            path = path.Replace("/", ".").Substring(1,path.Length-1);
            if (!_pathToAssetMap.ContainsKey(path))
                return null;

            var resourceDescriptor = _pathToAssetMap[path];
            return new Asset(
                GetEmbeddedResourceStreamFor(resourceDescriptor),
                InferMediaTypeFrom(resourceDescriptor.Name)
            );
        }

        private Stream GetEmbeddedResourceStreamFor(EmbeddedAssetDescriptor resourceDescriptor)
        {
            var stream = resourceDescriptor.Assembly.GetManifestResourceStream(resourceDescriptor.Name);
            if (stream == null)
                throw new FileNotFoundException(String.Format("Embedded resource not found - {0}", resourceDescriptor.Name));


            return stream;
        }

        private static string InferMediaTypeFrom(string path)
        {
            var extension = path.Split('.').Last();

            switch (extension)
            {
                case "ico":
                    return "image/x-icon";
                case "css":
                    return "text/css";
                case "js":
                    return "text/javascript";
                case "gif":
                    return "image/gif";
                case "png":
                    return "image/png";
                case "eot":
                    return "application/vnd.ms-fontobject";
                case "woff":
                    return "application/font-woff";
                case "woff2":
                    return "application/font-woff2";
                case "otf":
                    return "application/font-sfnt"; // formerly "font/opentype"
                case "ttf":
                    return "application/font-sfnt"; // formerly "font/truetype"
                case "svg":
                    return "image/svg+xml";
                default:
                    return "text/html";
            }
        }
    }
}
