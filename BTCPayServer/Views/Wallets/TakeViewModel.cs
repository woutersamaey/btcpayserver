using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTCPayServer.Views.Wallets
{
    public class TakeViewModel
    {
        [Display(Name = "Maker's URI")]
        [UriAttribute]
        [Required]
        public string MakerUri { get; set; }
    }
}
