// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using Model;
using System;
using System.IO;
using Cecil = Mono.Cecil;
namespace CecilProvider
{
    public class Loader : ILoader
    {
        private readonly Host ourHost;

        public Loader(Host host)
        {
            ourHost = host;
        }

        public void Dispose() { }

        public Host Host => ourHost;

        public Assembly LoadCoreAssembly()
        {
            // the core assembly can be different between assemblies
            throw new NotImplementedException();
        }

        public Assembly LoadAssembly(string fileName)
        {
            Cecil.ModuleDefinition module = Cecil.ModuleDefinition.ReadModule(fileName);
            Cecil.DefaultAssemblyResolver assemblyResolver = module.AssemblyResolver as Cecil.DefaultAssemblyResolver;
            assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(fileName)));
            Assembly assembly = ExtractAssembly(module);
            ourHost.Assemblies.Add(assembly);
            return assembly;
        }

        private Assembly ExtractAssembly(Cecil.ModuleDefinition module)
        {
            AssemblyExtractor extractor = new AssemblyExtractor(module, ourHost);
            return extractor.ExtractAssembly();
        }
    }
}
