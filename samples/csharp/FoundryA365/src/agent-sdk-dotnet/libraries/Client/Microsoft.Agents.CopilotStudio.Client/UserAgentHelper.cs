using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.CopilotStudio.Client
{
    internal static class UserAgentHelper
    {
        private static object _initLock = new object();
        public static string ClientFileVersion { get; private set; } = "unknown";

        public static string ClientName { get; private set; } = "CopilotStudioClient";

        public static string UserAgentHeader { get; private set; }

        private static ProductInfoHeaderValue? _frameworkProductInfo = null;
        private static ProductInfoHeaderValue? _versionString = null;
        private static ProductInfoHeaderValue? _osString = null;

        static UserAgentHelper()
        {
            // This is the expected order for our User-Agent.
            var userAgent = new List<ProductInfoHeaderValue>
            {
                GetClientProductInfo(),
                GetFrameworkProductInfo(),
                GetOSProductInfo()
            };

            var userAgentValue = string.Empty;
            foreach (var productInfo in userAgent)
            {
                if (string.IsNullOrEmpty(userAgentValue))
                {
                    userAgentValue = productInfo.ToString();
                }
                else
                {
                    userAgentValue += " " + productInfo.ToString();
                }
            }
            UserAgentHeader = userAgentValue;
        }

        private static ProductInfoHeaderValue GetClientProductInfo()
        {
            if (_versionString != null)
            {
                return _versionString;
            }
            _versionString = new ProductInfoHeaderValue($"{ClientName}.agents-sdk-net", ThisAssembly.AssemblyFileVersion);
            return _versionString;
        }

        private static ProductInfoHeaderValue GetOSProductInfo()
        {
            if (_osString != null)
            {
                return _osString;
            }

            var os = Environment.OSVersion;

            _osString = new ProductInfoHeaderValue(os.Platform.ToString(), $"{os.Version.Major}.{os.Version.Minor}.{os.Version.Build}");
            return _osString;
        }

        private static readonly Regex FrameworkRegEx = new Regex(@"(?:(\d+)\.)?(?:(\d+)\.)?(?:(\d+)\.\d+)", RegexOptions.Compiled);

        private static ProductInfoHeaderValue GetFrameworkProductInfo()
        {
            if (_frameworkProductInfo != null)
                return _frameworkProductInfo;

            var frameworkName = Assembly
                    .GetEntryAssembly()?
                    .GetCustomAttribute<TargetFrameworkAttribute>()?
                    .FrameworkName ?? RuntimeInformation.FrameworkDescription;

            var splitFramework = frameworkName.Replace(",", string.Empty).Replace(" ", string.Empty).Split('=');
            if (splitFramework.Length > 1)
            {
                _ = ProductInfoHeaderValue.TryParse($"{splitFramework[0]}/{splitFramework[1]}", out _frameworkProductInfo);
            }
            else
            {
                frameworkName = splitFramework[0];

                // Parse the version from the framework string.
                var version = FrameworkRegEx.Match(frameworkName);

                if (version.Success)
                {
                    frameworkName = frameworkName.Replace(version.Value, string.Empty).Trim();
                    _ = ProductInfoHeaderValue.TryParse($"{frameworkName}/{version.Value}", out _frameworkProductInfo);
                }
            }

            return _frameworkProductInfo!;
        }
    }
}
