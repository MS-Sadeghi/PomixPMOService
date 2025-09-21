using System.ComponentModel.DataAnnotations;

namespace PomixPMOService.API.Models.ViewModels
{
    public class ApproveTextViewModel
    {
        [Required]
        public bool IsApproved { get; set; }
    }
}
