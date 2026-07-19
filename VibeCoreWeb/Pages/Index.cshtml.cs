using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VibeCore.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => Redirect("/app");
}
