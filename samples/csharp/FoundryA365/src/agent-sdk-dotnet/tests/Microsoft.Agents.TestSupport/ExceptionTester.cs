using Microsoft.Agents.Core.Errors;
using System;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Agents.TestSupport
{
    public static class ExceptionTester
    {
        /// <summary>
        /// Checks the exception for the requested type and then logs the content to the output window. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e"></param>
        /// <param name="output"></param>
        public static void IsException<T>(Exception e, int expectedErrorCode,  ITestOutputHelper output)
        {
            StringBuilder stringBuilder = new(1024);
            int level = 0;
            e.GetExceptionDetail(stringBuilder, level);
            output.WriteLine(stringBuilder.ToString());

            Assert.IsType<T>(e);
            Assert.Equal(expectedErrorCode, e.HResult);

        }
    }
}
