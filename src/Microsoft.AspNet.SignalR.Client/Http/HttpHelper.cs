// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Infrastructure;

namespace Microsoft.AspNet.SignalR.Client.Http
{
    internal static class HttpHelper
    {
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exceptions are flowed back to the caller.")]
        public static Task<HttpWebResponse> GetHttpResponseAsync(this HttpWebRequest request)
        {
            try
            {
                return Task.Factory.FromAsync<HttpWebResponse>(request.BeginGetResponse, ar => (HttpWebResponse)request.EndGetResponse(ar), null);
            }
            catch (Exception ex)
            {
                return TaskAsyncHelper.FromError<HttpWebResponse>(ex);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exceptions are flowed back to the caller.")]
        public static Task<Stream> GetHttpRequestStreamAsync(this HttpWebRequest request)
        {
            try
            {
                return Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null);
            }
            catch (Exception ex)
            {
                return TaskAsyncHelper.FromError<Stream>(ex);
            }
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, Action<HttpRequestMessage> requestPreparer)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            if (requestPreparer != null)
            {
                requestPreparer(request);
            }
            return client.SendAsync(request);
        }

        public static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, Action<HttpRequestMessage> requestPreparer, IDictionary<string, string> postData)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);

            if (requestPreparer != null)
            {
                requestPreparer(request);
            }

            byte[] buffer = ProcessPostData(postData);

            request.Method = HttpMethod.Post;
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Set the content length if the buffer is non-null
            request.Content.Headers.ContentLength = buffer != null ? buffer.LongLength : 0;

            if (buffer != null)
            {
                request.Content = new ByteArrayContent(buffer);
            }

            return client.SendAsync(request);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.Text.StringBuilder.AppendFormat(System.String,System.Object[])", Justification = "This will never be localized.")]
        private static byte[] ProcessPostData(IDictionary<string, string> postData)
        {
            if (postData == null || postData.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var pair in postData)
            {
                if (sb.Length > 0)
                {
                    sb.Append("&");
                }

                if (String.IsNullOrEmpty(pair.Value))
                {
                    continue;
                }

                sb.AppendFormat("{0}={1}", pair.Key, UrlEncoder.UrlEncode(pair.Value));
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

    }
}
