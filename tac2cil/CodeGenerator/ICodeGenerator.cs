using Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    interface ICodeGenerator
    {
        Host Host { get; }
        void GenerateAssemblies(string pathToFolder);
    }
}
