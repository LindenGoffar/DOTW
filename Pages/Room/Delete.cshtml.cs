using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DOTW.Data;
using DOTW.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DOTW
{
    [Authorize]
    public class DeleteModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly DOTW.Data.DotwDbContext _context;

        public DeleteModel(UserManager<IdentityUser> userManager, DOTW.Data.DotwDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public Room Room { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Room = await _context.Room.FirstOrDefaultAsync(m => m.ID == id);

            if (Room == null)
            {
                return NotFound();
            }

            // Get our logged in user: 
            var user = await _userManager.GetUserAsync(User);
            if (Room.HostEmail.ToLower() != user.Email.ToLower())
            {
                return RedirectToPage("Error", new { Error = "Only a Host may delete a room" });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Room = await _context.Room.FindAsync(id);

            if (Room != null)
            {
                // Make sure our user is host. 
                var user = await _userManager.GetUserAsync(User);
                if (Room.HostEmail.ToLower() != user.Email.ToLower())
                {
                    return RedirectToPage("Error", new { Error = "Only a Host may delete a room" });
                }
                // We've got alot of objects to delete first.
                // Order Chairs -> Players -> Teams -> Rows -> Room
                _context.Entry(Room)
                         .Collection(r => r.Rows)
                         .Load();
                foreach (Row row in Room.Rows)
                {
                    _context.Entry(row)
                                .Collection(r => r.Chairs)
                                .Load();
                    foreach (Chair chair in row.Chairs)
                    {
                        _context.Chair.Remove(chair);
                    }
                }
                await _context.SaveChangesAsync();

                // Remove Players
                _context.Entry(Room)
                        .Collection(r => r.Players)
                        .Load();
                foreach (Player player in Room.Players)
                {
                    _context.Player.Remove(player);
                }
                await _context.SaveChangesAsync();

                // Remove Teams
                List<Team> teams = await _context.Team.Where(t => t.RoomId == Room.ID).ToListAsync();
                foreach (Team team in teams)
                {
                    _context.Team.Remove(team);
                }
                await _context.SaveChangesAsync();

                // Remove Rows
                foreach (Row row in Room.Rows)
                {
                    _context.Row.Remove(row);
                }
                await _context.SaveChangesAsync();

                // and finally remove the room. 
                _context.Room.Remove(Room);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
