using System;

namespace TSCodegen.WebAPI
{
    [Obsolete("Method should be refactored to be working without the attribute.")]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class CodegenSuppressErrorsAttribute : Attribute
    {
    }
}
