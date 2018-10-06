using System;
using System.Net.Http;
using BTCPayServer.AtomicSwaps;

namespace BTCPayServer.Services
{
    public class AtomicSwapClientFactory
    {
        public AtomicSwapClientFactory(IHttpClientFactory httpClientFactory)
        {
            HttpClientFactory = httpClientFactory;
        }

        public IHttpClientFactory HttpClientFactory { get; }

        public AtomicSwapClient Create(Uri serverUri)
        {
            var client = new AtomicSwapClient(serverUri);
            client.SetClient(HttpClientFactory.CreateClient());
            return client;
        }
    }
}
