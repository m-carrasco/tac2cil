using Model;
using System.Collections.Generic;

namespace CodeGenerator
{
    internal interface ICodeGenerator<T>
    {
        Host Host { get; }
        ICollection<T> GenerateAssemblies();
        void WriteAssemblies(string pathToFolder);
    }
}
