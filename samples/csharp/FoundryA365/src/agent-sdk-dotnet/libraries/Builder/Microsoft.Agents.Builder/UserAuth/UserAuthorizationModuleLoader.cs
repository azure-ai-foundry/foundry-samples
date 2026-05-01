// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.UserAuth.AgenticAuth;
using Microsoft.Agents.Builder.UserAuth.Connector;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
#if !NETSTANDARD
using System.Runtime.Loader;
#endif

namespace Microsoft.Agents.Builder.UserAuth
{
#if !NETSTANDARD
    internal class UserAuthorizationModuleLoader( AssemblyLoadContext loadContext, ILogger logger)
    {
        private readonly AssemblyLoadContext _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
#else
    internal class UserAuthorizationModuleLoader( AppDomain loadContext, ILogger logger)
    {
        private readonly AppDomain _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
#endif

        public ConstructorInfo GetProviderConstructor(string name, string assemblyName, string typeName)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(name, nameof(name));

            if (string.IsNullOrEmpty(assemblyName))
            {
                // A Assembly Lib name wasn't given in config.  Set to the default assembly lib
                assemblyName = typeof(AzureBotUserAuthorization).Assembly.GetName().Name;
                logger.LogInformation("No assembly name given in config for connection `{name}`.  Using default assembly lib: `{assemblyName}`", name, assemblyName);
            }

            if (string.IsNullOrEmpty(typeName))
            {
                // A Type name wasn't given in config.  Set to the default type name
                typeName = typeof(AzureBotUserAuthorization).FullName;
                logger.LogInformation("No type name given in config for connection `{name}`.  Using default type name: `{typeName}`", name, typeName);
            }
            else if (string.Equals(nameof(AzureBotUserAuthorization), typeName, StringComparison.OrdinalIgnoreCase))
            {
                typeName = typeof(AzureBotUserAuthorization).FullName;
            }
            else if (string.Equals(nameof(AgenticUserAuthorization), typeName, StringComparison.OrdinalIgnoreCase))
            {
                typeName = typeof(AgenticUserAuthorization).FullName;
            }
            else if (typeName.Equals(nameof(ConnectorUserAuthorization), StringComparison.OrdinalIgnoreCase))
            {
                typeName = typeof(ConnectorUserAuthorization).FullName;
            }
            
            // This throws for invalid assembly name.
#if !NETSTANDARD
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
                    throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationTypeNotFound, null, typeName, assemblyName, name);
                }
            }
            return GetConstructor(type) ?? throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateUserAuthorizationHandler, null, typeName, assemblyName); 
        }

        public IEnumerable<ConstructorInfo> GetProviderConstructors(string assemblyName)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(assemblyName, nameof(assemblyName));

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
                !typeof(IUserAuthorization).IsAssignableFrom(type) ||
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
                types: [typeof(string), typeof(IStorage), typeof(IConnections), typeof(IConfigurationSection), typeof(ILogger)],
                modifiers: null);
        }
    }
}
