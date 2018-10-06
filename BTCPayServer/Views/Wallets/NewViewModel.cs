using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.SqlServer.Server;

namespace BTCPayServer.Views.Wallets
{
    public class NamedWallet
    {
        public string Name { get; set; }
        public string CryptoCode { get; set; }
        public WalletId WalletId { get; set; }
        public Rating.RateRule Rule { get; set; }
        public decimal Spread { get; set; }
        public DerivationStrategy DerivationStrategy { get; set; }
    }
    public class NewViewModel
    {
        public class NameWalletObj
        {
            public string CryptoCode { get; set; }
            public decimal Spread { get; set; }
            public string Rule { get; set; }
        }
        [Display(Name = "To wallet")]
        public string SelectedWallet { get; set; }

        [Display(Name = "Amount to receive in the destination wallet (or rating rule)")]
        [Required()]
        public string RateRule { get; set; }

        [Range(0, double.MaxValue)]
        [Display(Name = "Amount to send from this wallet")]
        public double Amount { get; set; }

        public string CryptoCode { get; set; }

        [Display(Name = "Add a spread on exchange rate of ... %")]
        [Range(0.0, 100.0)]
        public double Spread
        {
            get;
            set;
        }

        public Dictionary<WalletId, NameWalletObj> WalletData { get; set; }

        public SelectList WalletList { get; set; }

        public void SetWalletList(NamedWallet[] namedWallet, string selectedWallet)
        {
            var choices = namedWallet.Select(o => new { Name = o.Name, Value = o.WalletId.ToString() }).ToArray();
            var chosen = choices.FirstOrDefault(f => f.Value == selectedWallet) ?? choices.FirstOrDefault();
            WalletList = new SelectList(choices, nameof(chosen.Value), nameof(chosen.Name), chosen);
            SelectedWallet = chosen.Value;
            WalletData = namedWallet.ToDictionary(o => o.WalletId, o => new NameWalletObj() { CryptoCode = o.CryptoCode, Rule = o.Rule.ToString(), Spread = o.Spread });
        }
    }
}
