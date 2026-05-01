// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Attribute containing a type with a LoadHeaders method to be called when initializing header propagation.
/// </summary>
/// <param name="type">Declared type containing static LoadHeaders method to call.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class HeaderPropagationAssemblyAttribute(Type type) : Attribute
{
    public readonly Type InitType = type;

    internal static void InitHeaderPropagation()
    {
        // Register handler for new assembly loads. This is needed because
        // C# doesn't load a package until accessed.
        AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);

        // Call header propagation init on currently loaded assemblies.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            InitAssembly(assembly);
        }
    }

    private static void InitAssembly(Assembly assembly)
    {
        foreach (var type in GetLoadOnInitTypes(assembly))
        {
#if !NETSTANDARD
            if (!typeof(IHeaderPropagationAttribute).IsAssignableFrom(type))
            {
                throw new InvalidOperationException($"Type '{type.FullName}' is marked with [HeaderPropagation] but does not implement IHeaderPropagationAttribute.");
            }
#endif

            var init = type.GetMethod("LoadHeaders", BindingFlags.Static | BindingFlags.Public);

            if (init == null)
            {
                continue;
            }

            init.Invoke(null, [HeaderPropagationContext.HeadersToPropagate]);
        }
    }

    private static IEnumerable<Type> GetLoadOnInitTypes(Assembly assembly) =>
        assembly
            .GetCustomAttributes(typeof(HeaderPropagationAssemblyAttribute), false)?
            .OfType<HeaderPropagationAssemblyAttribute>()
            .Select(x => x.InitType)
            ?? Array.Empty<Type>();
}
