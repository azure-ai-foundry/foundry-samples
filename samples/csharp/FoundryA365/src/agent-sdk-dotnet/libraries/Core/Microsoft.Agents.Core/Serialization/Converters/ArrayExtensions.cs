// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal static partial class ArrayExtensions
    {
        public static T[,] To2D<T>(this List<List<T>> source)
        {
            // Adapted from this answer https://stackoverflow.com/a/26291720/3744182
            // By https://stackoverflow.com/users/3909293/diligent-key-presser
            // To https://stackoverflow.com/questions/26291609/converting-jagged-array-to-2d-array-c-sharp
            var firstDim = source.Count;
            var secondDim = source.Select(row => row.Count).FirstOrDefault();
            if (!source.All(row => row.Count == secondDim))
                throw new InvalidOperationException();
            var result = new T[firstDim, secondDim];
            for (var i = 0; i < firstDim; i++)
                for (int j = 0, count = source[i].Count; j < count; j++)
                    result[i, j] = source[i][j];
            return result;
        }
    }
}
