using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTCPayServer.Views.Wallets
{
    public class AtomicSwapDetailsTakerWaitingTakerViewModel
    {
        public string ToSend { get; set; }
        public string ToReceive { get; internal set; }
        public string WalletId { get; set; }
        public Controllers.WalletsController.RefundTime RefundTime { get; set; }

        [Display(Name = "Receive on wallet...")]
        public string SelectedWallet { get; set; }
        public SelectList WalletList { get; set; }

        public void SetWalletList(NamedWallet[] namedWallet, string selectedWallet)
        {
            var choices = namedWallet.Select(o => new { Name = o.Name, Value = o.WalletId.ToString() }).ToArray();
            var chosen = choices.FirstOrDefault(f => f.Value == selectedWallet) ?? choices.FirstOrDefault();
            WalletList = new SelectList(choices, nameof(chosen.Value), nameof(chosen.Name), chosen);
            SelectedWallet = chosen.Value;
        }
    }
}
