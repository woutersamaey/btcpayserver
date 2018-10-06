using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Views.Wallets;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using NBitcoin;
using NBitcoin.JsonConverters;
using NBXplorer;
using Newtonsoft.Json;

namespace BTCPayServer.AtomicSwaps
{
    public class AtomicSwapClient
    {
        public AtomicSwapClient(Uri serverAddress)
        {
            if (serverAddress == null)
                throw new ArgumentNullException(nameof(serverAddress));
            ServerAddress = serverAddress;
        }

        private static readonly HttpClient SharedClient = new HttpClient();
        internal HttpClient Client = SharedClient;

        public void SetClient(HttpClient client)
        {
            Client = client;
        }
        public Uri ServerAddress { get; }

        internal string GetFullUri(string relativePath, params object[] parameters)
        {
            relativePath = string.Format(CultureInfo.InvariantCulture, relativePath, parameters ?? Array.Empty<object>());
            var uri = ServerAddress.AbsoluteUri;
            if (!uri.EndsWith("/", StringComparison.Ordinal))
                uri += "/";
            uri += relativePath;
            return uri;
        }
        private Task<T> GetAsync<T>(string relativePath, object[] parameters, CancellationToken cancellation)
        {
            return SendAsync<T>(HttpMethod.Get, null, relativePath, parameters, cancellation);
        }
        private async Task<T> SendAsync<T>(HttpMethod method, object body, string relativePath, object[] parameters, CancellationToken cancellation)
        {
            HttpRequestMessage message = CreateMessage(method, body, relativePath, parameters);
            var result = await Client.SendAsync(message, cancellation).ConfigureAwait(false);
            if ((int)result.StatusCode == 404)
            {
                return default(T);
            }
            return await ParseResponse<T>(result).ConfigureAwait(false);
        }

        internal HttpRequestMessage CreateMessage(HttpMethod method, object body, string relativePath, object[] parameters)
        {
            var uri = GetFullUri(relativePath, parameters);
            var message = new HttpRequestMessage(method, uri);
            if (body != null)
            {
                if (body is byte[])
                    message.Content = new ByteArrayContent((byte[])body);
                else
                    message.Content = new StringContent(NBitcoin.JsonConverters.Serializer.ToString(body), Encoding.UTF8, "application/json");
            }

            return message;
        }

        private async Task<T> ParseResponse<T>(HttpResponseMessage response)
        {
            using (response)
            {
                if (response.IsSuccessStatusCode)
                    if (response.Content.Headers.ContentLength == 0)
                        return default(T);
                    else if (response.Content.Headers.ContentType.MediaType.Equals("application/json", StringComparison.Ordinal))
                    {
                        var str = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return NBitcoin.JsonConverters.Serializer.ToObject<T>(str);
                    }
                    else if (response.Content.Headers.ContentType.MediaType.Equals("application/octet-stream", StringComparison.Ordinal))
                    {
                        return (T)(object)await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return default(T);
                var aaa = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                //if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                response.EnsureSuccessStatusCode();
                //var error = _Serializer.ToObject<NBXplorerError>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                //if (error == null)
                //    response.EnsureSuccessStatusCode();
                //throw error.AsException();
                return default(T);
            }
        }

        private Task ParseResponse(HttpResponseMessage response)
        {
            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return Task.CompletedTask;
                //if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                response.EnsureSuccessStatusCode();
                //var error = _Serializer.ToObject<NBXplorerError>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                //if (error == null)
                //    response.EnsureSuccessStatusCode();
                //throw error.AsException();
                return Task.CompletedTask;
            }
        }

        internal async Task<AtomicSwapOffer> GetOffer(CancellationToken cancellation = default)
        {
            return await SendAsync<AtomicSwapOffer>(HttpMethod.Get, null, "offer", null, cancellation);
        }

        public async Task<AtomicSwapTakeResponse> Take(AtomicSwapTakeRequest atomicSwapTakeRequest, CancellationToken cancellation = default)
        {
            return await SendAsync<AtomicSwapTakeResponse>(HttpMethod.Post, atomicSwapTakeRequest, "take", null, cancellation);
        }
    }
}
