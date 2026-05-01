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
    public class EntityInitAssemblyAttribute(Type type) : Attribute
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
                try
                {
                    var entityNameOverride = type.GetCustomAttribute<EntityNameAttribute>(false);
                    if (entityNameOverride != null)
                    {
                        ProtocolJsonSerializer.EntityTypes[entityNameOverride.EntityName] = type;
                        continue;
                    }
                    else
                        ProtocolJsonSerializer.EntityTypes[type.Name] = type;
                }
                catch (Exception)
                {
                    // ignore errors, likely duplicate keys
                    // TODO: log this
                }
            }
        }

        private static IEnumerable<Type> GetLoadOnInitTypes(Assembly assembly) =>
            assembly
                .GetCustomAttributes(typeof(EntityInitAssemblyAttribute), false)?
                .OfType<EntityInitAssemblyAttribute>()
                .Select(x => x.InitType)
                ?? Array.Empty<Type>();
    }
}
