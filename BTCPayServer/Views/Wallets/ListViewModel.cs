using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BTCPayServer.Views.Wallets
{
    public class ListViewModel
    {
        public class SwapItem
        {
            public string OfferId { get; set; }
            public string WalletId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public string Partner { get; set; }
            public string Sent { get; set; }
            public string Received { get; set; }
            public string Status { get; set; }
            public string Role { get; set; }
        }
        public string CryptoCode { get; set; }
        public List<SwapItem> Swaps { get; set; } = new List<SwapItem>();
    }
}
