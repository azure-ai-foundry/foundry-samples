// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Attribute containing a type with an Init method to be called when initializing <see cref="Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer"/>.
    /// </summary>
    /// <param name="type">Declared type containing static Init method to call.</param>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class SerializationInitAssemblyAttribute(Type type) : Attribute
    {
        public readonly Type InitType = type;

        internal static void InitSerialization()
        {
            // Register handler for new assembly loads.  This is needed because
            // C# doesn't load a package until accessed.
            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);

            // Call serialization init on currently loaded assemblies.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                InitAssembly(assembly);
            }
        }

        private static void InitAssembly(Assembly assembly)
        {
            foreach (var type in GetLoadOnInitTypes(assembly))
            {
                var init = type.GetMethod("Init", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                init?.Invoke(null, null);
            }
        }

        private static IEnumerable<Type> GetLoadOnInitTypes(Assembly assembly) =>
            assembly
                .GetCustomAttributes(typeof(SerializationInitAssemblyAttribute), false)?
                .OfType<SerializationInitAssemblyAttribute>()
                .Select(x => x.InitType)
                ?? Array.Empty<Type>();
    }
}
