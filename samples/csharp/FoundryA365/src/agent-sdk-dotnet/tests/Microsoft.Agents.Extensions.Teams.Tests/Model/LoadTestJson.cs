// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    internal static class LoadTestJson
    {
        public static string LoadJson(object obj, string variant = null)
        {
            var filename = variant == null ? obj.GetType().Name : $"{obj.GetType().Name}_{variant}";
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Model/TestJson", $"{filename}.json"));
        }
    }
}
