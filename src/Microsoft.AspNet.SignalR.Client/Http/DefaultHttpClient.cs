// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.AspNet.SignalR.Client.Http
{
    /// <summary>
    /// The default <see cref="IHttpClient"/> implementation.
    /// </summary>
    public class DefaultHttpClient : IHttpClient
    {
        private HttpClient _httpClient;

        private readonly string _shortRunningGroup;
        private readonly string _longRunningGroup;

        private IConnection _connection;
        


        public DefaultHttpClient(HttpClient httpClient)
        {
            string id = Guid.NewGuid().ToString();
            _shortRunningGroup = "SignalR-short-running-" + id;
            _longRunningGroup = "SignalR-long-running-" + id;
            _httpClient = httpClient;

        }

        public DefaultHttpClient() : this (new HttpClient())
        {
            
        }

        public void Initialize(IConnection connection)
        {
            _connection = connection;
        }

        /// <summary>
        /// Makes an asynchronous http GET request to the specified url.
        /// </summary>
        /// <param name="url">The url to send the request to.</param>
        /// <param name="prepareRequest">A callback that initializes the request with default values.</param>
        /// <param name="isLongRunning">Indicates whether the request is long running</param>
        /// <returns>A <see cref="T:Task{IResponse}"/>.</returns>
        public Task<IResponse> Get(string url, Action<IRequest> prepareRequest, bool isLongRunning)
        {
            return HttpHelper.GetAsync(this._httpClient, url, request =>
            {
                // Todo: Find out where to put the connectiongroup name
                //request.ConnectionGroupName = isLongRunning ? _longRunningGroup : _shortRunningGroup;

                var req = new HttpRequestMessageWrapper(request, () => {});
                prepareRequest(req);
                PrepareClientRequest(req);
            }
            ).Then(response => (IResponse)new HttpResponseMessageWrapper(response));
        }

        /// <summary>
        /// Makes an asynchronous http POST request to the specified url.
        /// </summary>
        /// <param name="url">The url to send the request to.</param>
        /// <param name="prepareRequest">A callback that initializes the request with default values.</param>
        /// <param name="postData">form url encoded data.</param>
        /// <param name="isLongRunning">Indicates whether the request is long running</param>
        /// <returns>A <see cref="T:Task{IResponse}"/>.</returns>
        public Task<IResponse> Post(string url, Action<IRequest> prepareRequest, IDictionary<string, string> postData, bool isLongRunning)
        {
            return HttpHelper.PostAsync(_httpClient, url, request =>
            {
                //request.ConnectionGroupName = isLongRunning ? _longRunningGroup : _shortRunningGroup;
                // Todo.. figure out abort
                var req = new HttpRequestMessageWrapper(request, () => { });
                prepareRequest(req);
                PrepareClientRequest(req);
            },
            postData).Then(response => (IResponse)new HttpResponseMessageWrapper(response));
        }

        /// <summary>
        /// Adds certificates, credentials, proxies and cookies to the request
        /// </summary>
        /// <param name="req">Request object</param>
        private void PrepareClientRequest(HttpRequestMessageWrapper req)
        {
            // Todo: set these properties
#if NET4
            if (_connection.Certificates != null)
            {
                //req.AddClientCerts(_connection.Certificates);
            }
#endif

            if (_connection.CookieContainer != null)
            {
                //req.CookieContainer = _connection.CookieContainer;
            }

            if (_connection.Credentials != null)
            {
                //req.Credentials = _connection.Credentials;
            }

            if (_connection.Proxy != null)
            {
                //req.Proxy = _connection.Proxy;
            }
        }
    }
}
