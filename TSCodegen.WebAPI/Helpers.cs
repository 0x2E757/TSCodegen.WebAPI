using System;
using System.IO;

namespace TSCodegen.WebAPI
{
    internal static class Helpers
    {
        public static DirectoryInfo GetSolutionRootDir()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);

            while (dir.GetFiles("*.sln").Length == 0)
                if (dir == null)
                    throw new DirectoryNotFoundException("Solution root directory could not be found.");
                else
                    dir = dir.Parent;

            return dir;
        }

        public static string GetControllerName(Type type)
        {
            if (type.Name.EndsWith("Controller"))
                return type.Name.Substring(0, type.Name.Length - 10);

            return type.Name;
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
