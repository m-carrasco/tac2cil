// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using Model;
using Model.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cecil = Mono.Cecil;
namespace CecilProvider
{
    public class Loader : ILoader
    {
        private Host ourHost;

        public Loader(Host host)
        {
            this.ourHost = host;
        }

        public void Dispose() {}

        public Host Host
        {
            get { return ourHost; }
        }

        public Assembly LoadCoreAssembly()
        {
            // the core assembly can be different between assemblies
            throw new NotImplementedException();
        }

        public Assembly LoadAssembly(string fileName)
        {
            var module = Cecil.ModuleDefinition.ReadModule(fileName);
            var assemblyResolver = module.AssemblyResolver as Cecil.DefaultAssemblyResolver;
            assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(fileName)));
            var assembly = this.ExtractAssembly(module);
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
