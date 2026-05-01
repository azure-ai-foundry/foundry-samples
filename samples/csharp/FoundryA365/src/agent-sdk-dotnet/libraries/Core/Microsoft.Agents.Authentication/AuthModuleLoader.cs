// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Errors;
using Microsoft.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
#if !NETSTANDARD
using System.Runtime.Loader;
#endif


namespace Microsoft.Agents.Authentication
{
#if !NETSTANDARD
    internal class AuthModuleLoader(AssemblyLoadContext loadContext, ILogger logger)
    {
        private readonly AssemblyLoadContext _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
#else
    internal class AuthModuleLoader(AppDomain loadContext, ILogger logger)
    {
        private readonly AppDomain _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
#endif

        // Default auth lib for AgentSDK authentication.  
        private readonly string _defaultAuthenticationLib = "Microsoft.Agents.Authentication.Msal"; 
        private readonly string _defaultAuthenticationLibEntryType = "MsalAuth";

        public ConstructorInfo GetProviderConstructor(string name, string assemblyName, string typeName)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            if (string.IsNullOrEmpty(assemblyName))
            {
                // A Assembly Lib name wasn't given in config.  Set to the default assembly lib
                logger.LogInformation("No assembly name given in config for connection `{name}`.  Using default assembly lib: `{assemblyName}`", name, _defaultAuthenticationLib);
                assemblyName = _defaultAuthenticationLib; 
            }



            if (string.IsNullOrEmpty(typeName))
            {
                // A Type name wasn't given in config.  Set to the default type name
                logger.LogInformation("No type name given in config for connection `{name}`.  Using default type name: `{typeName}`", name, _defaultAuthenticationLibEntryType);
                typeName = _defaultAuthenticationLibEntryType;
            }

#if !NETSTANDARD
            // This throws for invalid assembly name.
            Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));
#else
            // This throws for invalid assembly name.
            Assembly assembly = _loadContext.Load(assemblyName);
#endif

            Type type = assembly.GetType(typeName);
            if (!IsValidProviderType(type))
            {
                // Perhaps config left off the full type name?
                type = assembly.GetType($"{assemblyName}.{typeName}");
                if (!IsValidProviderType(type))
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.AuthProviderTypeNotFound, null, typeName, assemblyName, name);
                }
            }
            return GetConstructor(type) ?? throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateAuthModuleProvider, null, typeName, assemblyName); 
        }

        public IEnumerable<ConstructorInfo> GetProviderConstructors(string assemblyName)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(assemblyName, nameof(assemblyName));

#if !NETSTANDARD
            Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));
#else
            Assembly assembly = _loadContext.Load(assemblyName);
#endif

            foreach (Type loadedType in assembly.GetTypes())
            {
                if (!IsValidProviderType(loadedType))
                {
                    continue;
                }

                ConstructorInfo constructor = GetConstructor(loadedType);
                if (constructor == null)
                {
                    continue;
                }

                yield return constructor;
            }
        }

        private static bool IsValidProviderType(Type type)
        {
            if (type == null ||
                !typeof(IAccessTokenProvider).IsAssignableFrom(type) ||
                !type.IsPublic ||
                type.IsNested ||
                type.IsAbstract)
            {
                return false;
            }

            return true;
        }

        private static ConstructorInfo GetConstructor(Type type)
        {
            return type.GetConstructor(
                bindingAttr: BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: [typeof(IServiceProvider), typeof(IConfigurationSection)],
                modifiers: null);
        }
    }
}