using Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    interface ICodeGenerator<T>
    {
        Host Host { get; }
        ICollection<T> GenerateAssemblies();
        void WriteAssemblies(string pathToFolder);
    }
}
