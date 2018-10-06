using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.AtomicSwaps;
using BTCPayServer.Data;
using BTCPayServer.Models;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Bitcoin;
using BTCPayServer.Rating;
using BTCPayServer.Services;
using BTCPayServer.Services.Apps;
using BTCPayServer.Services.Rates;
using BTCPayServer.Services.Stores;
using BTCPayServer.Views.Wallets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;


namespace BTCPayServer.Controllers
{
    [Route("api/xswap")]
    public partial class AtomicSwapController : Controller
    {
        public AtomicSwapController(AtomicSwapRepository atomicSwapRepository, AtomicSwapClientFactory atomicSwapClientFactory)
        {
            AtomicSwapRepository = atomicSwapRepository;
            AtomicSwapClientFactory = atomicSwapClientFactory;
        }

        public AtomicSwapRepository AtomicSwapRepository { get; }
        public AtomicSwapClientFactory AtomicSwapClientFactory { get; }

        [Route("{offerId}/offer")]
        public async Task<IActionResult> GetOfferAPI(string offerId)
        {
            var entry = await AtomicSwapRepository.GetEntry(offerId);
            if (entry == null ||
                (entry.Status != XSwapStatus.WaitingTaker && entry.Role == XSwapRole.Maker) ||
                (entry.Status != XSwapStatus.WaitingEscrow && entry.Role == XSwapRole.Taker))
                return NotFound();
            return Json(entry.Offer);
        }

        [Route("{offerId}/take")]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> TakeOfferAPI(string offerId, [FromBody] AtomicSwapTakeRequest request)
        {
            var entry = await AtomicSwapRepository.GetEntry(offerId);
            if (entry == null || entry.Status != XSwapStatus.WaitingTaker)
                return NotFound();
            // TODO atomically take the offer
            var client = AtomicSwapClientFactory.Create(request.TakerUri);
            AtomicSwapTakeResponse response = null;
            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    cts.CancelAfter(5000);
                    var takerOffer = await client.GetOffer(cts.Token);
                    if (takerOffer.MarketMakerUri != entry.Offer.MarketMakerUri)
                        return NotFound();
                }
            }
            catch { }

            entry.Partner = request.TakerUri.DnsSafeHost;
            entry.OtherUri = request.TakerUri;
            entry.Status = XSwapStatus.WaitingEscrow;
            entry.Sent.MyKey = new Key();
            entry.Sent.OtherKey = request.MakerSentCryptoPubkey;
            entry.Received.MyKey = new Key();
            entry.Received.OtherKey = request.MakerReceivedCryptoPubkey;
            entry.Preimage = new Preimage();
            entry.Hash = entry.Preimage.GetHash();

            response = new AtomicSwapTakeResponse()
            {
                Hash = entry.Preimage.GetHash(),
                MakerReceivedCryptoPubkey = entry.Received.MyKey.PubKey,
                MakerSentCryptoPubkey = entry.Received.MyKey.PubKey
            };

            await AtomicSwapRepository.UpdateEntry(offerId, entry);
            return Json(response);
        }
    }
}
