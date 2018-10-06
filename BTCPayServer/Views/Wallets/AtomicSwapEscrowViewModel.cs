using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BTCPayServer.Views.Wallets
{
    public class AtomicSwapEscrowViewModel
    {
        public string ToSend { get; set; }
        public string ToReceive { get; internal set; }
        public string SentToWalletId { get; set; }
        public Controllers.WalletsController.RefundTime RefundTime { get; set; }
        public string SentFromWalletId { get; set; }
        public string EscrowAddress { get; internal set; }
        public string Amount { get; set; }
    }
}
