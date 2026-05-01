// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Agents.Core
{
    public static class AssertionHelpers
    {

        public static void ThrowIfNull(object obj, string name)
        {
#if !NETSTANDARD
            ArgumentNullException.ThrowIfNull(obj, name);
#else
            if (obj == null)
            {
                throw new ArgumentNullException(name);
            }
#endif
        }
        public static void ThrowIfNullOrWhiteSpace(string str, string name)
        {
#if !NETSTANDARD    
            ArgumentException.ThrowIfNullOrWhiteSpace(str, name);
#else
            if (str == null)
                throw new ArgumentNullException(name);

            if (string.IsNullOrWhiteSpace(str))
            {
                throw new ArgumentException(name);
            }
#endif
        }
        public static void ThrowIfNullOrEmpty(string str, string name)
        {
#if !NETSTANDARD    
            ArgumentException.ThrowIfNullOrEmpty(str, name);
#else
            if (str == null)
                throw new ArgumentNullException(name);

            if (string.IsNullOrWhiteSpace(str))
            {
                throw new ArgumentException(name);
            }
#endif
        }

        public static void ThrowIfNullOrEmpty(ICollection collection, string name)
        {
            if (collection == null)
                throw new ArgumentNullException(name);

            if (collection.Count == 0)
            {
                throw new ArgumentException(name);
            }
        }

        public static void ThrowIfNullOrEmpty<TKey, TValue>(IDictionary<TKey, TValue> dict, string name)
        {
            if (dict == null)
                throw new ArgumentNullException(name);

            if (dict.Count == 0)
            {
                throw new ArgumentException(name);
            }
        }

        public static void ThrowIfObjectDisposed(bool obj, string name)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(obj, name);
#else
            if (obj)
            {
                throw new ObjectDisposedException(name);
            }
#endif
        }
    }
}
