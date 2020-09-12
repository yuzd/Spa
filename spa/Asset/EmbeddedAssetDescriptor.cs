using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace spa.Asset
{
    public class EmbeddedAssetDescriptor
    {
        public EmbeddedAssetDescriptor(Assembly containingAssembly, string name, bool isTemplate = false)
        {
            Assembly = containingAssembly;
            Name = name;
            IsTemplate = isTemplate;
        }

        public Assembly Assembly { get; private set; }

        public string Name { get; private set; }

        public bool IsTemplate { get; private set; }
    }
}
