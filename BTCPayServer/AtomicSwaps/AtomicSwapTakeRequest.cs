using System;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace BTCPayServer.AtomicSwaps
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNeverAttribute]
    public class AtomicSwapTakeRequest
    {
        [JsonConverter(typeof(KeyJsonConverter))]
        public PubKey MakerSentCryptoPubkey { get; set; }
        [JsonConverter(typeof(KeyJsonConverter))]
        public PubKey MakerReceivedCryptoPubkey { get; set; }
        public Uri TakerUri { get; set; }
    }
}
