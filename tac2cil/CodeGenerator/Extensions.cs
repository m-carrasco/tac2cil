using System;
using System.Collections.Generic;
using System.Text;

namespace CodeGenerator
{
    public static class Extensions
    {
        // todo: once analysis-net works correctly regarding generic parameter count and arguments, we could use analysis-net's extension
        public static string MetadataName(this Model.Types.IBasicType basicType)
        {
            if (basicType.GenericParameterCount == 0)
                return basicType.Name;

            var arguments = string.Empty;

            if (basicType.GenericArguments.Count > 0)
            {
                arguments = string.Join(", ", basicType.GenericArguments);
                arguments = string.Format("<{0}>", arguments);
            }

            return string.Format("{0}`{1}{2}", basicType.Name, basicType.GenericParameterCount, arguments);
        }
    }
}
