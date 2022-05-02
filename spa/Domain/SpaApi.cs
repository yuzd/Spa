using System;

namespace spa.Domain;


[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class SpaApi:System.Attribute
{
    public SpaApi(string name)
    {
        this.Name = name;
    }
    /// <summary>
    /// 针对某个路径需要指定supper用户才有权限，*代表只有supper用户才有权限
    /// </summary>
    public string SupperRequired { get; set; }
    /// <summary>
    /// true代表不需要检查权限所有人都可以访问，有的是在方法内部做的权限校验
    /// </summary>
    public bool NotCheck { get; set; }
    public string Name { get; set; }
}