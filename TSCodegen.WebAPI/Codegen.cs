using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TSCodegen.WebAPI
{
    public static class Codegen
    {
        public class Config
        {
            public string OutputPath { get; set; }
            public string AxiosImportPath { get; set; }
            public int Indentation { get; set; } = 4;
            public List<Type> IgnoreControllers { get; set; } = new List<Type>();
            public List<string> ForbiddenNamespaces { get; set; } = new List<string>();
        }

        private static string[] HttpReadTypes { get; } = new string[] { "GET", "DELETE", "HEAD", "OPTIONS" };
        private static string[] HttpWriteTypes { get; } = new string[] { "POST", "PUT", "PATCH" };

        private static Config CurrentConfig { get; set; }
        private static Assembly CodegenAssembly { get; set; }
        private static Assembly ControllersAssembly { get; set; }
        private static string AbsoluteOutputPath { get; set; }

        private static List<string> Header { get; set; }
        private static List<string> FileNames { get; set; }

        private static IEnumerable<Type> Controllers { get; set; }
        private static Type CurrentController { get; set; }
        private static TypeScriptTypes ControllerTypeScriptTypes { get; set; }
        private static IEnumerable<MethodInfo> HttpMethods { get; set; }
        private static MethodInfo CurrentHttpMethod { get; set; }
        private static ParameterInfo[] CurrentHttpMethodParameters { get; set; }
        private static string CurrentHttpMethodType { get; set; }
        private static string CurrentHttpMethodAlias { get; set; }

        private static string IndentSpaces => new string(' ', CurrentConfig.Indentation);
        private static bool CurrentHttpMethodHasParameters => CurrentHttpMethodParameters.Length > 0;
        private static bool CurrentHttpMethodIsReadType => HttpReadTypes.Contains(CurrentHttpMethodType);
        private static bool CurrentHttpMethodIsWriteType => HttpWriteTypes.Contains(CurrentHttpMethodType);

        public static void Run(Config config)
        {
            CurrentConfig = config;
            CodegenAssembly = Assembly.GetExecutingAssembly();
            ControllersAssembly = Assembly.GetCallingAssembly();
            AbsoluteOutputPath = Helpers.GetSolutionRootDir().FullName + config.OutputPath;
            GenerateHeader();
            GenerateFileNamesList();
            GenerateServices();
            DeleteExcessFiles();
        }

        private static void GenerateHeader()
        {
            var dashes = new string('/', 3);
            var spaces = new string(' ', 3);

            var text1 = $"Auto-generated file by {CodegenAssembly.GetName().Name} in {ControllersAssembly.GetName().Name}, do not edit!";
            var text2 = $"GitHub: https://github.com/0x2E757/TSCodegen.WebAPI";
            text2 += new string(' ', text1.Length - text2.Length);

            var str1 = new string('/', text1.Length + (dashes.Length + spaces.Length) * 2);
            var str2 = dashes + new string(' ', text1.Length + spaces.Length * 2) + dashes;
            var str3 = dashes + spaces + text1 + spaces + dashes;
            var str4 = dashes + spaces + text2 + spaces + dashes;

            Header = new List<string> { str1, str2, str3, str4, str2, str1 };
        }

        private static void GenerateFileNamesList(string path = null)
        {
            if (path == null)
            {
                FileNames = new List<string>();
                path = AbsoluteOutputPath;

                if (!Directory.Exists(path))
                    return;
            }

            foreach (var file in Directory.GetFiles(path))
                FileNames.Add(file);

            foreach (var subDirectory in Directory.GetDirectories(path))
                GenerateFileNamesList(subDirectory);
        }

        private static IEnumerable<Type> GetControllers()
        {
            var types = ControllersAssembly.GetTypes();

            return Controllers = types.Where(t => t.GetCustomAttributes(typeof(ApiControllerAttribute)).Any());
        }

        private static IEnumerable<MethodInfo> GetHttpMethods()
        {
            var methods = CurrentController.GetMethods();

            return HttpMethods = methods.Where(m => m.GetCustomAttributes().Any(ca => ca.GetType().Name.StartsWith("Http")));
        }

        private static void GetCurrentHttpMethodParameters()
        {
            CurrentHttpMethodParameters = CurrentHttpMethod.GetParameters();
        }

        private static void GetCurrentHttpMethodType()
        {
            var httpAttribute = CurrentHttpMethod.GetCustomAttributes().First(ca => ca.GetType().Name.StartsWith("Http"));

            CurrentHttpMethodType = httpAttribute.GetType().Name.Substring(4).Replace("Attribute", "").ToUpper();
        }

        private static void GetCurrentHttpMethodAlias()
        {
            var nameIsUnique = HttpMethods.Where(hm => hm.Name == CurrentHttpMethod.Name).Count() == 1;

            if (nameIsUnique || CurrentHttpMethodType == "POST")
                CurrentHttpMethodAlias = CurrentHttpMethod.Name;
            else
                CurrentHttpMethodAlias = CurrentHttpMethodType.ToLower() + CurrentHttpMethod.Name;
        }

        private static List<string> GenerateHttpMethodParameterStrings()
        {
            var parameterStrings = new List<string>();
            var allParametersAreNullable = true;

            foreach (var parameter in CurrentHttpMethodParameters.Reverse())
            {
                var typeScriptParameterType = new TypeScriptType(parameter.ParameterType);

                if (!typeScriptParameterType.IsNullable)
                    allParametersAreNullable = false;

                if (allParametersAreNullable)
                    parameterStrings.Add(parameter.Name + (typeScriptParameterType.IsNullable ? "?" : "") + ": " + typeScriptParameterType.GetFullTypeName());
                else
                    parameterStrings.Add(parameter.Name + ": " + typeScriptParameterType.GetFullTypeName() + (typeScriptParameterType.IsNullable ? " | undefined" : ""));
            }

            return parameterStrings;
        }

        private static List<string> GenerateHttpMethodFunctionVariables()
        {
            if (CurrentHttpMethodHasParameters)
            {
                if (CurrentHttpMethodIsReadType)
                    if (CurrentHttpMethodParameters[0].GetCustomAttributes(typeof(FromBodyAttribute)).Any())
                        return new List<string>()
                        {
                            $"{IndentSpaces}const data = {{ {CurrentHttpMethodParameters[0].Name} }};",
                        };
                    else
                        return new List<string>()
                        {
                            $"{IndentSpaces}const params = {{ {string.Join(", ", CurrentHttpMethodParameters.Select(p => p.Name))} }};",
                            $"{IndentSpaces}const paramsSerializer = (params: any) => qs.stringify(params, {{ allowDots: true }});",
                        };

                if (CurrentHttpMethodIsWriteType)
                    if (CurrentHttpMethodParameters[0].GetCustomAttributes(typeof(FromFormAttribute)).Any())
                        return new List<string>()
                        {
                            $"{IndentSpaces}const headers = {{ \"Content-Type\": \"multipart/form-data\" }};",
                            $"{IndentSpaces}const config = {{ headers }};",
                        };
                    else if (CurrentHttpMethodParameters.Length > 1)
                        return new List<string>()
                        {
                            $"{IndentSpaces}const params = {{ {string.Join(", ", CurrentHttpMethodParameters.Select(p => p.Name))} }};",
                        };
            }

            return new List<string>();
        }

        private static string GenerateHttpMethodAxiosArguments()
        {
            var result = $"url";

            if (CurrentHttpMethodHasParameters)
            {
                if (CurrentHttpMethodIsReadType)
                    if (CurrentHttpMethodParameters[0].GetCustomAttributes(typeof(FromBodyAttribute)).Any())
                        return result += $", {{ data }}";
                    else
                        return result += $", {{ params, paramsSerializer }}";

                if (CurrentHttpMethodIsWriteType)
                    if (CurrentHttpMethodParameters.Length == 1)
                        if (CurrentHttpMethodParameters[0].GetCustomAttributes(typeof(FromFormAttribute)).Any())
                            return result += $", {CurrentHttpMethodParameters[0].Name}, config";
                        else
                            return result += $", {CurrentHttpMethodParameters[0].Name}";
                    else
                        return result += $", null, {{ params }}";
            }

            return result;
        }

        private static List<string> GetRelatedBaseTypeNameList(TypeScriptType typeScriptType)
        {
            var result = new List<string>() { };

            while (typeScriptType.HasElement)
                typeScriptType = typeScriptType.Element;

            if (typeScriptType.HasDeclaration)
            {
                result.Add(typeScriptType.BaseTypeName);

                if (typeScriptType.IsClass && typeScriptType.IsGeneric)
                    foreach (var genericArgument in typeScriptType.GenericArguments)
                        if (genericArgument.HasDeclaration)
                            result.AddRange(GetRelatedBaseTypeNameList(genericArgument));
            }

            return result;
        }

        private static List<string> GenerateHttpMethodFile()
        {
            var strings = new List<string>();

            var typeScriptReturnType = new TypeScriptType(CurrentHttpMethod.ReturnType);
            var parameterStrings = GenerateHttpMethodParameterStrings();
            var functionVariables = GenerateHttpMethodFunctionVariables();

            strings.Add($"import axios from \"{CurrentConfig.AxiosImportPath}\";");

            if (functionVariables.Where(fv => fv.Contains("qs.stringify")).Any())
                strings.Add($"import qs from \"qs\";");

            strings.Add($"");

            foreach (var parameter in CurrentHttpMethodParameters)
            {
                var typeScriptParameterType = new TypeScriptType(parameter.ParameterType);

                foreach (var baseTypeName in GetRelatedBaseTypeNameList(typeScriptParameterType))
                    strings.Add($"import {{ {baseTypeName} }} from \"./types\";");
            }

            foreach (var baseTypeName in GetRelatedBaseTypeNameList(typeScriptReturnType))
                strings.Add($"import {{ {baseTypeName} }} from \"./types\";");

            if (strings.Last() != "")
                strings.Add($"");

            if (CurrentHttpMethodHasParameters && CurrentHttpMethodIsReadType)
            {
                strings.Add($"export interface I{CurrentHttpMethodAlias.ToPascalCase()}Args {{");

                foreach (var parameter in CurrentHttpMethodParameters)
                {
                    var typeScriptType = new TypeScriptType(parameter.ParameterType);

                    strings.Add($"{IndentSpaces}{parameter.Name}{(typeScriptType.IsNullable ? "?" : "")}: {typeScriptType.GetFullTypeName()};");
                }

                strings.Add($"}}");
                strings.Add($"");
            }

            strings.Add($"export default async ({string.Join(", ", parameterStrings.ToArray().Reverse())}) => {{");
            strings.Add($"{IndentSpaces}const url = \"{Helpers.GetControllerName(CurrentController)}/{CurrentHttpMethod.Name}\";");
            strings.AddRange(functionVariables);
            strings.Add($"{IndentSpaces}const response = await axios.{CurrentHttpMethodType.ToLower()}<{typeScriptReturnType.GetFullTypeName()}>({GenerateHttpMethodAxiosArguments()});");
            strings.Add($"{IndentSpaces}return response.data;");
            strings.Add($"}}");
            strings.Add($"");

            return strings;
        }

        private static void WriteFile(string fileName, List<string> strings)
        {
            var text = string.Join("\n", Header) + "\n\n" + string.Join("\n", strings);

            if (!text.EndsWith("\n"))
                text += "\n";

            // Newlines can be converted from \n to \r\n, to handle that replace all \r\n to \n before comparison
            if (File.Exists(fileName) ? File.ReadAllText(fileName).Replace("\r\n", "\n") != text : true)
                File.WriteAllText(fileName, text);

            FileNames.Remove(fileName);
        }

        private static void GenerateServices()
        {
            var controllerNames = new List<string>();

            foreach (var controller in GetControllers())
            {
                if (CurrentConfig.IgnoreControllers.Contains(controller))
                    continue;

                var controllerIndexLines = new List<string>();

                CurrentController = controller;
                ControllerTypeScriptTypes = new TypeScriptTypes(CurrentConfig.ForbiddenNamespaces);

                var serviceDirName = AbsoluteOutputPath + @"\" + Helpers.GetControllerName(CurrentController).ToCamelCase();

                foreach (var httpMethod in GetHttpMethods())
                {
                    CurrentHttpMethod = httpMethod;

                    GetCurrentHttpMethodParameters();
                    GetCurrentHttpMethodType();
                    GetCurrentHttpMethodAlias();

                    ControllerTypeScriptTypes.Add(CurrentHttpMethodParameters.Select(p => new TypeScriptType(p.ParameterType)));
                    ControllerTypeScriptTypes.Add(new TypeScriptType(CurrentHttpMethod.ReturnType));

                    var httpMethodLines = GenerateHttpMethodFile();

                    Helpers.EnsureDirectoryExists(serviceDirName);
                    WriteFile(serviceDirName + @"\" + CurrentHttpMethodAlias.ToCamelCase() + ".ts", httpMethodLines);

                    controllerIndexLines.Add($"export {{ default as {CurrentHttpMethodAlias.ToCamelCase()} }} from \"./{CurrentHttpMethodAlias.ToCamelCase()}\";");
                }

                WriteFile(serviceDirName + @"\types.ts", ControllerTypeScriptTypes.GetDeclarations(CurrentConfig.Indentation, true));

                controllerIndexLines.Add("export * from \"./types\";");

                WriteFile(serviceDirName + @"\index.ts", controllerIndexLines);

                controllerNames.Add(Helpers.GetControllerName(CurrentController).ToCamelCase());
            }

            var indexLines = new List<string>();

            indexLines.AddRange(controllerNames.Select(controllerName => $"import * as {controllerName} from \"./{controllerName}\";"));
            indexLines.Add("");
            indexLines.Add("export default {");
            indexLines.Add(string.Join($"\n", controllerNames.Select(cn => $"{IndentSpaces}{cn},")));
            indexLines.Add("};");

            WriteFile(AbsoluteOutputPath + @"\index.ts", indexLines);
        }

        private static void DeleteExcessFiles()
        {
            foreach (var fileName in FileNames)
                File.Delete(fileName);
        }
    }
}
