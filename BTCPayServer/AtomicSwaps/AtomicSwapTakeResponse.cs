using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace BTCPayServer.AtomicSwaps
{
    public class AtomicSwapTakeResponse
    {
        [JsonConverter(typeof(UInt160JsonConverter))]
        public uint160 Hash { get; set; }
        [JsonConverter(typeof(KeyJsonConverter))]
        public PubKey MakerSentCryptoPubkey { get; set; }
        [JsonConverter(typeof(KeyJsonConverter))]
        public PubKey MakerReceivedCryptoPubkey { get; set; }
    }
}
