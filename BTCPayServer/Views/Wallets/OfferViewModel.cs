using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BTCPayServer.Views.Wallets
{
    public class OfferViewModel
    {
        public string Title { get; set; }

        public string EscrowAddress { get; set; }
        public string WalletUrl { get; set; }
        public string RefundTime { get; set; }
    }
}
