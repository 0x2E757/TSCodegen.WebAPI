# TSCodegen.WebAPI

Library for C# ASP.NET controllers conversion to TypeScript axios services. Available as [NuGet package](https://www.nuget.org/packages/TSCodegen.WebAPI/).

Based on [TSCodegen](https://github.com/0x2E757/TSCodegen) library.

Compatible with .NET Standard 2.0 and higher!

## How it works

`Codegen` will examine calling assembly for all classes with the `ApiController` attribute. Then for each matching class `Codegen` will iterate over all methods with any attribute that starts with "Http" prefix (e.g. `HttpGet`).

`Codegen` will also handle methods with first argument having attribute `FromBody` for GET, DELETE, HEAD, OPTIONS methods and `FromForm` for POST, PUT, PATCH methods, though such methods must contain only one argument.

## Prerequisites

Install `axios` and `qs` (querystring) to your frontend project.

```Shell
npm i axios
npm i qs
```

## Usage

Add to the start of the `Main` function:

```C#
#if DEBUG
Codegen.Run(new Codegen.Config
{
    OutputPath = @"\ui\api",
    AxiosImportPath = @"axios",
    Indentation = 4,
    IgnoreControllers = new()
    {
        typeof(BaseController),
    },
    ForbiddenNamespaces = new()
    {
        "Project.Database.Core.Entities",
    }
});
#endif
```

### Codegen.Config properties

#### OutputPath

Output path that is relative to the `.sln` file.

#### AxiosImportPath

Path for `import` expression, e.g. with `axios` value will emit `import axios from "axios";`. Can be used to specify custom file with axios instance default export.

#### Indentation

Space indentation size for generated code.

#### IgnoreControllers

List of controllers that will be omited.

### ForbiddenNamespaces

List of namespaces that entities of will throw exception.

### DateTime conversion

C# `DateTime` class will be converted to `Date | DateTimeString`. You need to declare `DateTimeString` in any of your \*.d.ts files. Example:

```TypeScript
declare type DateString = "YYYY-MM-DD";
declare type TimeString = "HH:mm:ss";
declare type DateTimeString = `${DateString}T${TimeString}`;
```

### Generated files

Library will generate file structure:

```
api\
├ myController\
│  ├ myMethod1.ts
│  ├ myMethod2.ts
│  ├ types.ts
│  └ index.ts
└ index.ts
```

Where `api\index.ts` will contain default export with all APIs as objects with functions (e.g. `api.myController.myMethod`).
