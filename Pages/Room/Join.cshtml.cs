using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DOTW.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DOTW
{
    public class JoinModel : PageModel
    {
        private readonly DOTW.Data.DotwDbContext _context;

        public JoinModel(DOTW.Data.DotwDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public int RoomId { get; set; }
        public void OnGet()
        {
            ViewData["PlayerOptions"] = "Yes";
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (await _context.Room.Where(r => r.ID == RoomId).AnyAsync())
            {
                return RedirectToPage("Play", new { RoomId = RoomId });
            }
            return Page();
        }
    }
}