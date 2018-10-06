using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.AtomicSwaps;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Views.Wallets
{
    public class AtomicSwapRepository
    {
        ConcurrentDictionary<string, AtomicSwapEntry> _Offers = new ConcurrentDictionary<string, AtomicSwapEntry>();
        
        public Task SaveEntry(WalletId walletId, string offerId, AtomicSwapEntry entry)
        {
            entry.Id = offerId;
            _Offers.TryAdd(offerId, entry);
            _OfferIdsByWalletId.Add(walletId, offerId);
            return Task.CompletedTask;
        }

        public Task<AtomicSwapEntry> GetEntry(string offerId)
        {
            _Offers.TryGetValue(offerId, out var offer);
            return Task.FromResult(offer);
        }

        internal IEnumerable<AtomicSwapEntry> GetEntries(WalletId walletId)
        {
            if (!_OfferIdsByWalletId.TryGetValue(walletId, out var offers))
                return Array.Empty<AtomicSwapEntry>();
            return _OfferIdsByWalletId[walletId].Select(c => GetEntry(c).Result).OrderByDescending(o => o.Offer.CreatedAt);
        }

        MultiValueDictionary<WalletId, string> _OfferIdsByWalletId = new MultiValueDictionary<WalletId, string>();

        internal Task UpdateEntry(string offerId, AtomicSwapEntry entry)
        {
            _Offers.AddOrUpdate(offerId, entry, (k, oldv) => entry);
            return Task.CompletedTask;
        }
    }

    public enum XSwapRole
    {
        Maker,
        Taker
    }

    public enum XSwapStatus
    {
        WaitingTaker,
        WaitingEscrow,
        WaitingPeerEscrow,
        WaitingBlocks,
        CashingOut,
        Refunding,
        CashedOut,
        Refunded
    }

    public class AtomicSwapEntry
    {
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public XSwapRole Role { get; set; }
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public XSwapStatus Status { get; set; }
        public AtomicSwapOffer Offer { get; set; }
        public SentAtomicSwapAsset Sent { get; set; }
        public ReceivedAtomicSwapAsset Received { get; set; }
        public string Partner { get; set; }
        public Uri OtherUri { get; set; }
        public string Id { get; internal set; }

        public Preimage Preimage { get; set; }

        [JsonConverter(typeof(NBitcoin.JsonConverters.UInt160JsonConverter))]
        public uint160 Hash { get; set; }
    }

    public class AtomicSwapEscrowData
    {
        public AtomicSwapEscrowData(AtomicSwapOfferAsset offerAsset)
        {
            Amount = offerAsset.Amount;
            LockTime = offerAsset.LockTime;
            CryptoCode = offerAsset.CryptoCode;
        }
        public AtomicSwapEscrowData()
        {

        }
        public string CryptoCode { get; set; }
        public Key MyKey { get; set; }
        public PubKey OtherKey { get; set; }
        public LockTime LockTime { get; set; }
        public Money Amount { get; set; }
        public WalletId WalletId { get; set; }
        public EscrowScriptPubKeyParameters GetEscrow()
        {
            return new EscrowScriptPubKeyParameters(MyKey.PubKey, OtherKey, LockTime);
        }
    }

    public class ReceivedAtomicSwapAsset : AtomicSwapEscrowData
    {
        public ReceivedAtomicSwapAsset(AtomicSwapOfferAsset offerAsset) : base(offerAsset)
        {
        }
        public ReceivedAtomicSwapAsset()
        {

        }
        public Script Destination { get; set; }
    }

    public class SentAtomicSwapAsset : AtomicSwapEscrowData
    {
        public SentAtomicSwapAsset(AtomicSwapOfferAsset offerAsset) : base(offerAsset)
        {
        }
        public SentAtomicSwapAsset()
        {

        }
        public Script Refund { get; set; }
    }

    public class AtomicSwapOffer
    {
        [JsonConverter(typeof(Newtonsoft.Json.Converters.UnixDateTimeConverter))]
        public DateTimeOffset CreatedAt { get; set; }
        public string Rule { get; set; }
        public AtomicSwapOfferAsset Offer { get; set; }
        public AtomicSwapOfferAsset Price { get; set; }
        public Uri MarketMakerUri { get; set; }
    }

    public class AtomicSwapOfferAsset
    {
        public string CryptoCode { get; set; }
        [JsonConverter(typeof(NBitcoin.JsonConverters.LockTimeJsonConverter))]
        public LockTime LockTime { get; set; }
        [JsonConverter(typeof(NBitcoin.JsonConverters.MoneyJsonConverter))]
        public Money Amount { get; set; }
        [JsonConverter(typeof(NBitcoin.JsonConverters.KeyJsonConverter))]
        public PubKey Pubkey { get; set; }
    }
}
