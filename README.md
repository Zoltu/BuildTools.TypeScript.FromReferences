# Zoltu BuildTools: TypeScript from References

[![Join the chat at https://gitter.im/Zoltu/BuildTools.TypeScript.FromReferences](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/Zoltu/BuildTools.TypeScript.FromReferences?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![Build status](http://img.shields.io/appveyor/ci/Zoltu/buildtools-typescript-fromreferences.svg)](https://ci.appveyor.com/project/Zoltu/buildtools-typescript-fromreferences)
[![NuGet Status](http://img.shields.io/nuget/v/Zoltu.BuildTools.TypeScript.FromReferencesTask.svg)](https://www.nuget.org/packages/Zoltu.BuildTools.TypeScript.FromReferencesTask/)

The Problem
====
When working with TypeScript in Visual Studio, you may want to split your code up into several projects.  One project may be a library while another project is a full application.  Unfortunately, the Visual Studio build system currently does not support TypeScript projects, and referenced projects that have TypeScript items do not have the TypeScript files copied on build.

The Solution
============
This build tool can be added to any project as a NuGet package and when you build, all referenced projects (and the projects they reference) will be searched for .ts files.  When found, the .js, .js.map, .d.ts and .ts files will all be copied to a libraries folder in the project.  Also, the .ts file will be renamed to .ts.source so Visual Studio doesn't notice it (details below) and the .js.map file will be modified to point at the .ts.source file (so debugging works).

How to Use
==========
1. Create a TypeScript project (A).
 1. Add a TypeScript file to the project (MyModule.ts).
 1. In the project properties, set the TypeScript compiler to `generate a source maps` and `generate declaration files` and to use AMD or CommonJS.
1. Create another TypeScript project (B).
 1. Add Zoltu.BuildTools.TypeScript.FromReferences NuGet package to the project.
 1. Add a TypeScript file to the project (MyApp.ts).
 1. In the project properties, set the TypeScript compiler to use AMD or CommonJS (same choice as project A).
 1. Add a reference to project A.
 1. In MyApp.ts, import MyModule like so: `import MyModule = require("libraries/MyModule");`
1. Build the solution.

Why is `.ts` Renamed to `.ts.source`?
-----------------------------
If you were to copy `MyModule.ts` into project B, you would run into some subtle problems.  The primary one is that if you use Visual Studio's `Go To Definition` in `MyApp.ts`, it will take you to the .ts file.  At first, this sounds great, but the problem is that it takes you to the *copy* of `MyModule.ts` that is in the libraries folder of project B.  If you were to make changes to this file, your changes would be overwritten with the original `MyModule.ts` (from project A) as soon as you compiled again.  To work around this, we copy `MyModule.ts` to project B with a different name.

What happens when you use `Go To Definition` in Visual Studio then?  It will simply take you to the .d.ts file, which is much more obvious that you shouldn't modify.  You will still get full intellisense because the .d.ts contains all of the information necessary to populate the intellisense menus, you just don't see the full source code for your functions.

Why then do we copy `MyModule.ts` at all if we are just going to rename it so Visual Studio can't find it?  It is because we also modify `MyModule.js.map` to point at `MyModule.ts.source`.  This means that when you are debugging in Firefox, Chrome, Internet Explorer, or even Visual Studio you will get source mapping over to `MyModule.ts.source`!  This means while debugging you have full access to the source code, but while coding you only have easy-accidental-write-access to the definition file.

How to Change the Library Path
---------------------------
If you want the copied files to go somewhere other than $(ProjectDir)libraries then you just need to add a property to a PropertyGroup of your .csproj file like `<TypeScriptLibraryFullPath>app\scripts</TypeScriptLibraryFullPath>`.
