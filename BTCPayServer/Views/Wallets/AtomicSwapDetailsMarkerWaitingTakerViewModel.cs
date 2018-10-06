using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BTCPayServer.Views.Wallets
{
    public class AtomicSwapDetailsMarkerWaitingTakerViewModel
    {
        public string ToSend { get; set; }
        public string ToReceive { get; internal set; }
        public Uri OfferUri { get; set; }
    }
}
