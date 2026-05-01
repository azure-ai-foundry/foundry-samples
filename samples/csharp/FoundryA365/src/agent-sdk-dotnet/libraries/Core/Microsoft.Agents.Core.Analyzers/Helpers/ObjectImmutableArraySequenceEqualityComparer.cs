
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Agents.Core.Analyzers.Helpers
{
    internal class ObjectImmutableArraySequenceEqualityComparer<T> : IEqualityComparer<ImmutableArray<T?>>
        where T : class
    {
        public bool Equals(ImmutableArray<T?> left, ImmutableArray<T?> right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                bool areEqual = left[i] is { } leftElem
                    ? leftElem.Equals(right[i])
                    : right[i] is null;

                if (!areEqual)
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(ImmutableArray<T?> obj)
        {
            HashCode hash = default;
            for (int i = 0; i < obj.Length; i++)
            {
                hash.Add(obj[i]);
            }
            return hash.ToHashCode();
        }
    }

    internal sealed class ObjectImmutableArraySequenceEqualityComparer : ObjectImmutableArraySequenceEqualityComparer<object>
    {
        // This class is intentionally left empty. It exists to provide a non-generic version of the comparer.
        // The generic version is used for the actual comparison logic, while this serves as a specific type
        // for cases where the type parameter is not needed.
    }
}