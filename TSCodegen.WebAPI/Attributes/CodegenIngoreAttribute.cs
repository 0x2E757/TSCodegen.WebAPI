using System;

namespace TSCodegen.WebAPI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public class CodegenIgnoreAttribute : Attribute
    {
    }
}
