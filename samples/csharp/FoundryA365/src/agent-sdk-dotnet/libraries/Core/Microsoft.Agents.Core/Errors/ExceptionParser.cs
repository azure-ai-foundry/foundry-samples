// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace Microsoft.Agents.Core.Errors
{
    public static class ExceptionParser
    {
        /// <summary>
        /// Extracts detailed information about an exception, including its message, source, target site, help link, 
        /// error code, and optionally its stack trace, and appends it to the provided <see cref="System.Text.StringBuilder"/>.
        /// </summary>
        /// <param name="ex">The <see cref="System.Exception"/> instance to extract details from.</param>
        /// <param name="sw">The <see cref="System.Text.StringBuilder"/> to which the formatted exception details will be appended.</param>
        /// <param name="level">The depth of the exception in the hierarchy (e.g., 0 for the top-level exception, 1 for the first inner exception, etc.).</param>
        /// <param name="lastErrorMsg">
        /// An optional <see cref="System.Text.StringBuilder"/> to store the concatenated error messages from the exception and its inner exceptions.
        /// If not provided, a new instance will be created for the top-level exception.
        /// </param>
        /// <param name="includeStackTrace">Indicates whether the stack trace should be included in the output.</param>
        /// <remarks>
        /// This method recursively processes the exception and its inner exceptions, appending detailed information 
        /// about each exception to the provided <paramref name="sw"/>. It is useful for logging or debugging purposes.
        /// </remarks>
        public static void GetExceptionDetail(this Exception ex, StringBuilder sw, int level, StringBuilder? lastErrorMsg = null, bool includeStackTrace = false)
        {
            Exception generalEx = ex;
            bool firstIteration = false;
            if (level == 0)
            {
                sw.AppendLine();
                firstIteration = true;
                lastErrorMsg ??= new StringBuilder(1024);
            }
            
            string localErrorMessage = 
                generalEx is ErrorResponseException errorResponse ?
                DecodeErrorResponseExceptionMessage(errorResponse).ToString().Trim() : generalEx.Message.ToString().Trim();


            FormatExceptionMessage(
                generalEx.Source != null ? generalEx.Source.ToString().Trim() : "Not Provided",
                generalEx.TargetSite != null ? generalEx.TargetSite.Name.ToString() : "Not Provided",
                string.IsNullOrEmpty(generalEx.Message) ? "Not Provided" : localErrorMessage,
                string.IsNullOrEmpty(generalEx.HelpLink) ? "Not Provided" : generalEx.HelpLink.ToString().Trim(),
                generalEx.HResult == 0 ? null : generalEx.HResult,
                string.IsNullOrEmpty(generalEx.StackTrace) ? "Not Provided" : generalEx.StackTrace.ToString().Trim(),
                generalEx.Data,
                sw, level, includeStackTrace);

            lastErrorMsg.Append(string.IsNullOrEmpty(localErrorMessage) ? "Not Provided" : localErrorMessage.ToString().Trim());

            if (lastErrorMsg.Length > 0 && generalEx.InnerException != null)
                lastErrorMsg.Append(" => ");

            level++;
            if (generalEx.InnerException != null)
                GetExceptionDetail(generalEx.InnerException, sw, level, lastErrorMsg , includeStackTrace);

            if (firstIteration)
                sw.Insert(0, lastErrorMsg);
        }

        /// <summary>
        /// Formats and appends detailed exception information to the provided <see cref="System.Text.StringBuilder"/>.
        /// </summary>
        /// <param name="source">The source of the exception, typically the name of the application or object that caused the error.</param>
        /// <param name="targetSite">The name of the method where the exception occurred.</param>
        /// <param name="message">The message describing the exception.</param>
        /// <param name="helpLink">A URL to a help file or website that provides more information about the exception.</param>
        /// <param name="errorCode">The error code associated with the exception, if available.</param>
        /// <param name="stackTrace">The stack trace of the exception, if available.</param>
        /// <param name="exData">The additional data associated with the exception.</param>
        /// <param name="sw">The <see cref="System.Text.StringBuilder"/> to which the formatted exception details will be appended.</param>
        /// <param name="level">The depth of the exception in the hierarchy (e.g., 0 for the top-level exception, 1 for the first inner exception, etc.).</param>
        /// <param name="includeStackTrace">Indicates whether the stack trace should be included in the output.</param>
        /// <remarks>
        /// This method is used to create a detailed, human-readable representation of an exception, including its source, target site, message, 
        /// help link, error code, and optionally its stack trace. It is typically used for logging or debugging purposes.
        /// </remarks>
        private static void FormatExceptionMessage(string source, string targetSite, string message, string helpLink, int? errorCode, string stackTrace, IDictionary exData, StringBuilder sw, int level, bool includeStackTrace = false)
        {
            if (level != 0)
                sw.AppendLine($"Inner Exception Level {level}\t: ");
            if (errorCode.HasValue)
            {
                if (level == 0)
                    sw.AppendLine("=====================================");
                sw.AppendLine("ErrorCode: " + errorCode.Value);
            }
            sw.AppendLine("Source: " + source);
            sw.AppendLine("Method: " + targetSite);
            sw.AppendLine("TimeStamp: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sw.AppendLine("Error: " + message);
            sw.AppendLine($"HelpLink Url: {helpLink}");
            if (includeStackTrace)
            {
                foreach (DictionaryEntry de in exData)
                {
                    sw.AppendLine($"Headers: {de.Key} = {de.Value}");
                }
                //TODO:
                // Update this code to use a setting or environment variable to control the output of the stack trace.
                if (!string.IsNullOrEmpty(stackTrace))
                    sw.AppendLine("Stack Trace: " + stackTrace);
            }
            sw.AppendLine("=====================================");
        }

        /// <summary>
        /// Decodes the error message from an <see cref="Microsoft.Agents.Core.Errors.ErrorResponseException"/> instance.
        /// </summary>
        /// <param name="error">The <see cref="Microsoft.Agents.Core.Errors.ErrorResponseException"/> containing the error details to decode.</param>
        /// <returns>
        /// A string representing the decoded error message. If the <paramref name="error"/> or its body is null, 
        /// the method returns the exception's message. If the body contains an error, the method returns a formatted 
        /// string containing the exception message, remote error message, and error code.
        /// </returns>
        /// <remarks>
        /// This method handles cases where the <see cref="Microsoft.Agents.Core.Errors.ErrorResponseException"/> or its body is null, ensuring 
        /// that a meaningful error message is returned. If the body contains an error, it extracts and formats 
        /// the error details for better readability.
        /// </remarks>
        private static string DecodeErrorResponseExceptionMessage(ErrorResponseException error)
        {
            if (error != null)
            {
                if (error.Body == null)
                {
                    // Handle the case where Body or Error is null
                    return error.Message;
                }
                else if (error.Body.Error != null)
                {
                    // This is a specific error message
                    return string.Format("{0} - RemoteError: {1}, {2}", error.Message, error.Body.Error.Message, error.Body.Error.Code);
                }
                return error.Message;
            }
            return string.Empty;
        }
    }
}
