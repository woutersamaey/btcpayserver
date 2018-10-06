using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.AtomicSwaps;
using BTCPayServer.Data;
using BTCPayServer.ModelBinders;
using BTCPayServer.Rating;
using BTCPayServer.Services.Rates;
using BTCPayServer.Views.Wallets;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace BTCPayServer.Controllers
{

    public partial class WalletsController : Controller
    {
        [Route("{walletId}/xswap")]
        public async Task<IActionResult> AtomicSwapList([ModelBinder(typeof(WalletIdModelBinder))] WalletId walletId)
        {
            var derivationStrategy = await GetDerivationStrategy(walletId);
            if (derivationStrategy == null)
                return NotFound();
            ListViewModel list = new ListViewModel();
            list.CryptoCode = walletId.CryptoCode;
            foreach (var entry in AtomicSwapRepository.GetEntries(walletId))
            {
                ListViewModel.SwapItem item = new ListViewModel.SwapItem()
                {
                    WalletId = walletId.ToString(),
                    OfferId = entry.Id,
                    Partner = entry.Partner,
                    Role = entry.Role.ToString(),
                    Status = entry.Status.ToString(),
                    Timestamp = entry.Offer.CreatedAt,
                    Sent = FormatAmount(entry.Sent),
                    Received = FormatAmount(entry.Received),
                };
                list.Swaps.Add(item);
            }
            return View(list);
        }

        private string FormatAmount(AtomicSwapEscrowData entry)
        {
            return _currencyTable.DisplayFormatCurrency(entry.Amount.ToDecimal(MoneyUnit.BTC), entry.CryptoCode);
        }

        [Route("{walletId}/xswap/new")]
        public async Task<IActionResult> NewAtomicSwap([ModelBinder(typeof(WalletIdModelBinder))] WalletId walletId)
        {
            var derivationStrategy = await GetDerivationStrategy(walletId);
            if (derivationStrategy == null)
                return NotFound();
            var storeData = await Repository.FindStore(walletId.StoreId, GetUserId());
            var wallets = await GetNamedWallets(walletId.CryptoCode);
            var newVM = new NewViewModel();
            var rateRules = storeData.GetStoreBlob().GetRateRules(NetworkProvider);
            newVM.SetWalletList(wallets, walletId.ToString());
            newVM.CryptoCode = walletId.CryptoCode;
            return View(newVM);
        }

        private async Task<NamedWallet[]> GetNamedWallets(string fromCrypto)
        {
            var stores = await Repository.GetStoresByUserId(GetUserId());
            return stores
                    .SelectMany(s => s.GetSupportedPaymentMethods(NetworkProvider)
                                    .OfType<DerivationStrategy>()
                                     .Where(p => p.Network.BlockTime != null)
                                     .Where(p => p != null && ExplorerClientProvider.IsAvailable(p.Network))
                                     .Select(p => new NamedWallet()
                                     {
                                         Name = $"{p.PaymentId.CryptoCode}: {s.StoreName}",
                                         DerivationStrategy = p,
                                         CryptoCode = p.PaymentId.CryptoCode,
                                         WalletId = new WalletId(s.Id, p.PaymentId.CryptoCode),
                                         Rule = GetRuleNoSpread(s.GetStoreBlob(), fromCrypto, p.PaymentId.CryptoCode),
                                         Spread = s.GetStoreBlob().Spread
                                     }))
                                     .ToArray();
        }

        private RateRule GetRuleNoSpread(StoreBlob storeBlob, string cryptoCodeA, string cryptoCodeB)
        {
            var rules = storeBlob.GetRateRules(NetworkProvider);
            rules.Spread = 0;
            var rule = rules.GetRuleFor(new CurrencyPair(cryptoCodeA, cryptoCodeB));
            return rule;
        }

        [TempData]
        public string StatusMessage { get; set; }
        public string CreatedOfferId { get; private set; }

        [Route("{walletId}/xswap/new")]
        [HttpPost]
        public async Task<IActionResult> NewAtomicSwap(
            [ModelBinder(typeof(WalletIdModelBinder))]
            WalletId walletId,
            NewViewModel newVM)
        {
            var fromWallet = await GetDerivationStrategy(walletId);
            var statusAsync = ExplorerClientProvider.GetExplorerClient(fromWallet.Network).GetStatusAsync();
            if (fromWallet == null)
                return NotFound();
            var wallets = await GetNamedWallets(walletId.CryptoCode);
            newVM.SetWalletList(wallets, newVM.SelectedWallet);
            newVM.CryptoCode = fromWallet.Network.CryptoCode;
            if (!WalletId.TryParse(newVM.SelectedWallet, out var selectedWalletId))
            {
                ModelState.AddModelError(nameof(newVM.SelectedWallet), "Invalid wallet id");
                return View(newVM);
            }
            var toWallet = await GetDerivationStrategy(selectedWalletId);
            if (toWallet == null)
            {
                ModelState.AddModelError(nameof(newVM.SelectedWallet), "Invalid wallet id");
                return View(newVM);
            }

            var id = Encoders.Base58.EncodeData(RandomUtils.GetBytes(20));
            AtomicSwapOffer offer = new AtomicSwapOffer();
            offer.MarketMakerUri = new Uri($"{this.Request.GetAbsoluteRoot().WithTrailingSlash()}api/xswap/{id}", UriKind.Absolute);
            offer.Offer = new AtomicSwapOfferAsset()
            {
                Amount = Money.Coins((decimal)newVM.Amount),
                CryptoCode = walletId.CryptoCode,
            };

            var minRelayFee = (await statusAsync).BitcoinStatus.MinRelayTxFee;
            var minimumAmount = minRelayFee.GetFee(200); // Arbitrary but should cover the dust of any output
            if (offer.Offer.Amount <= minimumAmount)
            {
                ModelState.AddModelError(nameof(newVM.Amount), $"Amount must be above {minimumAmount}");
                return View(newVM);
            }
            offer.Price = new AtomicSwapOfferAsset()
            {
                CryptoCode = toWallet.PaymentId.CryptoCode
            };
            var lockTimespan = TimeSpan.FromDays(2);
            offer.CreatedAt = DateTimeOffset.UtcNow;

            var storeData = await Repository.FindStore(walletId.StoreId, GetUserId());
            if (ModelState.IsValid)
            {
                var pair = new CurrencyPair("AAA", "BBB");
                newVM.RateRule = $"{pair} = {newVM.RateRule}";

                if (RateRules.TryParse(newVM.RateRule, out var rules, out var rateRulesErrors))
                {
                    rules.Spread = (decimal)newVM.Spread / 100.0m;
                    var rateResult = await RateFetcher.FetchRate(pair, rules);
                    if (rateResult.BidAsk == null)
                    {
                        string errorMessage = "Error when fetching rate";
                        if (rateResult.EvaluatedRule != null)
                        {
                            errorMessage += $" ({rateResult.EvaluatedRule})";
                        }
                        ModelState.AddModelError(nameof(newVM.RateRule), errorMessage);
                    }
                    else
                    {
                        offer.Price.Amount = Money.Coins(offer.Offer.Amount.ToDecimal(MoneyUnit.BTC) * rateResult.BidAsk.Ask);

                        rules.Spread = 0;
                        offer.Rule = rules.GetRuleFor(pair).ToString();
                    }
                }
                else
                {
                    string errorDetails = "";
                    if (rateRulesErrors.Count > 0)
                    {
                        errorDetails = $" ({rateRulesErrors[0]})";
                    }
                    ModelState.AddModelError(nameof(newVM.RateRule), $"Impossible to parse rate rules{errorDetails}");
                }
            }
            if (!ModelState.IsValid)
                return View(newVM);

            var statusSent = ExplorerClientProvider.GetExplorerClient(offer.Offer.CryptoCode).GetStatusAsync();
            var statusReceived = ExplorerClientProvider.GetExplorerClient(offer.Price.CryptoCode).GetStatusAsync();

            offer.Offer.LockTime = new LockTime((await statusSent).ChainHeight + fromWallet.Network.GetBlockCount(lockTimespan));
            offer.Price.LockTime = new LockTime((await statusReceived).ChainHeight + toWallet.Network.GetBlockCount(lockTimespan));
            StatusMessage = $"Offer created, share the following link with the marker takers: {offer.MarketMakerUri}";
            CreatedOfferId = id;

            var entry = new AtomicSwapEntry();
            entry.Offer = offer;
            entry.Role = XSwapRole.Maker;
            entry.Status = XSwapStatus.WaitingTaker;
            entry.Sent = new SentAtomicSwapAsset(offer.Offer)
            {
                Refund = await GetDestination(fromWallet),
                WalletId = walletId,
            };
            entry.Received = new ReceivedAtomicSwapAsset(offer.Price)
            {
                Destination = await GetDestination(toWallet),
                WalletId = selectedWalletId
            };
            await AtomicSwapRepository.SaveEntry(walletId, id, entry);
            return RedirectToAction(nameof(AtomicSwapDetails), new { offerId = id, walletId = walletId.ToString() });
        }

        private async Task<Script> GetDestination(DerivationStrategy derivationStrategy)
        {
            var explorer = this.ExplorerClientProvider.GetExplorerClient(derivationStrategy.PaymentId.CryptoCode);
            var scriptPubKey = (await explorer.GetUnusedAsync(derivationStrategy.DerivationStrategyBase, NBXplorer.DerivationStrategy.DerivationFeature.Deposit, reserve: true)).ScriptPubKey;
            return scriptPubKey;
        }

        [Route("{walletId}/xswap/take")]
        public IActionResult TakeAtomicSwap(WalletId walletId)
        {
            return View(new TakeViewModel());
        }

        [Route("{walletId}/xswap/{offerId}/details")]
        public async Task<IActionResult> AtomicSwapDetails(
            [ModelBinder(typeof(WalletIdModelBinder))]
            WalletId walletId,
            string offerId)
        {
            var derivationStrategy = await GetDerivationStrategy(walletId);
            if (derivationStrategy == null)
                return NotFound();
            var offer = await AtomicSwapRepository.GetEntry(offerId);
            if (offer.Status == XSwapStatus.WaitingTaker)
            {
                if (offer.Role == XSwapRole.Maker)
                {
                    var vm = new AtomicSwapDetailsMarkerWaitingTakerViewModel()
                    {
                        ToSend = FormatAmount(offer.Sent),
                        ToReceive = FormatAmount(offer.Received),
                        OfferUri = new Uri($"{this.Request.GetAbsoluteRoot().WithTrailingSlash()}api/xswap/{offerId}", UriKind.Absolute)
                    };
                    return View("AtomicSwapDetailsMarkerWaitingTaker", vm);
                }
                if (offer.Role == XSwapRole.Taker)
                {
                    var vm = new AtomicSwapDetailsTakerWaitingTakerViewModel()
                    {
                        ToSend = FormatAmount(offer.Sent),
                        ToReceive = FormatAmount(offer.Received),
                        WalletId = walletId.ToString(),
                        RefundTime = await GetRefundTime(offer),
                    };
                    var wallets = (await GetNamedWallets(offer.Received.CryptoCode)).Where(w => w.DerivationStrategy.PaymentId.CryptoCode == offer.Received.CryptoCode).ToArray();
                    vm.SetWalletList(wallets, null);
                    return View("AtomicSwapDetailsTakerWaitingTaker", vm);
                }
            }
            else if (offer.Status == XSwapStatus.WaitingEscrow)
            {
                var network = NetworkProvider.GetNetwork(offer.Sent.CryptoCode);
                var vm = new AtomicSwapEscrowViewModel()
                {
                    ToSend = FormatAmount(offer.Sent),
                    ToReceive = FormatAmount(offer.Received),
                    SentToWalletId = walletId.ToString(),
                    SentFromWalletId = offer.Sent.WalletId.ToString(),
                    EscrowAddress = offer.Sent.GetEscrow().ToScript().GetDestinationAddress(network.NBitcoinNetwork).ToString(),
                    Amount = offer.Sent.Amount.ToString(),
                    RefundTime = await GetRefundTime(offer)
                };
                return View("AtomicSwapEscrow", vm);
            }
            return NotFound();
        }

        public class RefundTime
        {
            public int BlockCount { get; set; }
            public TimeSpan Time { get; set; }
        }
        private async Task<RefundTime> GetRefundTime(AtomicSwapEntry offer)
        {
            var status = await ExplorerClientProvider.GetExplorerClient(offer.Sent.CryptoCode).GetStatusAsync();
            var network = NetworkProvider.GetNetwork(offer.Sent.CryptoCode);
            var blocksToWait = Math.Max(0, offer.Sent.LockTime.Height - status.ChainHeight);
            var refundTime = network.GetTimeSpan(blocksToWait);
            return new RefundTime() { BlockCount = blocksToWait, Time = refundTime };
        }

        [Route("{walletId}/xswap/take")]
        [HttpPost]
        public async Task<IActionResult> TakeAtomicSwap([ModelBinder(typeof(WalletIdModelBinder))] WalletId walletId, TakeViewModel takeViewModel)
        {
            var derivationStrategy = await GetDerivationStrategy(walletId);
            var makerUri = new Uri(takeViewModel.MakerUri, UriKind.Absolute);
            AtomicSwapClient client = AtomicSwapClientFactory.Create(makerUri);
            var offer = await client.GetOffer();
            if (offer == null)
            {
                ModelState.AddModelError(nameof(takeViewModel.MakerUri), "Offer not found");
            }
            else
            {
                if (offer.Offer.CryptoCode != walletId.CryptoCode)
                {
                    ModelState.AddModelError(nameof(takeViewModel.MakerUri), $"Offer for {offer.Offer.CryptoCode}, but this wallet is for {walletId.CryptoCode}");
                }
            }
            if (!ModelState.IsValid)
                return View(takeViewModel);
            var entry = new AtomicSwapEntry()
            {
                Partner = new Uri(takeViewModel.MakerUri, UriKind.Absolute).DnsSafeHost,
                OtherUri = makerUri,
                Offer = offer,
                Received = new ReceivedAtomicSwapAsset(offer.Offer)
                {
                    OtherKey = offer.Offer.Pubkey,
                    Destination = await GetDestination(derivationStrategy),
                    WalletId = walletId
                },
                Sent = new SentAtomicSwapAsset(offer.Price)
                {
                    OtherKey = offer.Price.Pubkey,
                },
                Role = XSwapRole.Taker,
                Status = XSwapStatus.WaitingTaker
            };
            var id = Encoders.Base58.EncodeData(RandomUtils.GetBytes(20));
            CreatedOfferId = id;
            await AtomicSwapRepository.SaveEntry(walletId, id, entry);
            return RedirectToAction(nameof(AtomicSwapDetails), new { offerId = id, walletId = walletId.ToString() });
        }

        [Route("{walletId}/xswap/{offerId}/accept")]
        public async Task<IActionResult> AcceptAtomicSwapOffer(
            [ModelBinder(typeof(WalletIdModelBinder))] WalletId walletId,
            string offerId, AtomicSwapDetailsTakerWaitingTakerViewModel viewModel)
        {
            var derivationStrategy = await GetDerivationStrategy(walletId);
            if (derivationStrategy == null)
                return NotFound();

            var entry = await AtomicSwapRepository.GetEntry(offerId);
            if (entry == null)
                return NotFound();


            WalletId.TryParse(viewModel.SelectedWallet, out var selectedWalletId);
            var destinationStrategy = await GetDerivationStrategy(selectedWalletId);

            entry.Sent.WalletId = walletId;
            entry.Sent.Refund = await GetDestination(derivationStrategy);
            entry.Sent.MyKey = new Key();

            entry.Received.Destination = await GetDestination(destinationStrategy);
            entry.Received.WalletId = selectedWalletId;
            entry.Received.MyKey = new Key();
            entry.Status = XSwapStatus.WaitingEscrow;

            var maker = AtomicSwapClientFactory.Create(entry.OtherUri);
            var response = await maker.Take(new AtomicSwapTakeRequest()
            {
                TakerUri = new Uri($"{this.Request.GetAbsoluteRoot().WithTrailingSlash()}api/xswap/{entry.Id}", UriKind.Absolute),
                MakerReceivedCryptoPubkey = entry.Sent.MyKey.PubKey,
                MakerSentCryptoPubkey = entry.Received.MyKey.PubKey,
            });

            if (response == null)
                return NotFound();

            entry.Received.OtherKey = response.MakerSentCryptoPubkey;
            entry.Sent.OtherKey = response.MakerReceivedCryptoPubkey;
            entry.Hash = response.Hash;

            await AtomicSwapRepository.UpdateEntry(offerId, entry);

            return RedirectToAction(nameof(AtomicSwapDetails), new { offerId = offerId, walletId = walletId.ToString() });
        }

        async Task<DerivationStrategy> GetDerivationStrategy(WalletId walletId)
        {
            if (walletId?.StoreId == null || GetUserId() == null)
                return null;
            var storeData = await this.Repository.FindStore(walletId.StoreId, GetUserId());
            var strategy = storeData.GetSupportedPaymentMethods(NetworkProvider)
                     .OfType<DerivationStrategy>()
                     .FirstOrDefault(s => s.PaymentId.CryptoCode == walletId.CryptoCode);
            if (strategy == null || !ExplorerClientProvider.IsAvailable(strategy.Network))
                return null;
            return strategy;
        }
    }
}
