using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DOTW.Data;
using DOTW.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace DOTW
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly DOTW.Data.DotwDbContext _context;

        public IndexModel(UserManager<IdentityUser> userManager, DOTW.Data.DotwDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IList<Room> Room { get;set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            Room = await _context.Room.Where(r => r.HostEmail == user.Email).ToListAsync();
        }
    }
}
