using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using DOTW.Data;
using DOTW.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DOTW
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly DOTW.Data.DotwDbContext _context;

        public CreateModel(UserManager<IdentityUser> userManager, DOTW.Data.DotwDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public Room Room { get; set; }

        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            List<Player> PlayerList = new List<Player>();
            int PlayerCount = 0;
            string HostEmail;

            // Filter our player list to those with a valid name > 0 char
            PlayerList = Room.Players.Where(p => p.Name != null).ToList();
            PlayerCount = PlayerList.Count();

            // Get our logged in user: 
            var user = await _userManager.GetUserAsync(User);
            HostEmail = user.Email;

            // Add our Room
            var emptyRoom = new Room();

            if (await TryUpdateModelAsync<Room>(
                emptyRoom,
                "Room",   // Prefix for form value.
                r => r.RoomName))
            {
                emptyRoom.HostEmail = HostEmail;
                emptyRoom.PlayerCount = PlayerCount;
                emptyRoom.State = RoomState.TeamSelection;
                emptyRoom.Day = Day.Sunday; // all rooms start on Sunday
                _context.Room.Add(emptyRoom);
                await _context.SaveChangesAsync();

                // Room Added now let's add our Rows, Chairs, Teams, and Players in that order.
                // We are going to add one more chairs than may be needed per row. 
                int ChairsPerRow = emptyRoom.PlayerCount / 7 < 1 ? 2 : (emptyRoom.PlayerCount / 7) + 1;
                for (int r = 1; r <= 7; r++)
                {
                    Row row = new Row();
                    row.Position = r;
                    row.Day = (Day)r;
                    row.Room = emptyRoom;
                    row.State = RowState.AcceptingPlayers;
                    _context.Row.Add(row);
                    await _context.SaveChangesAsync();
                    // Add our chairs to this row. 
                    for (int c = 1; c <= ChairsPerRow; c++)
                    {
                        Chair chair = new Chair();
                        int position = r;
                        chair.Row = row;
                        chair.State = ChairState.Empty;
                        _context.Chair.Add(chair);
                        await _context.SaveChangesAsync();
                    }
                    // Add a team for this Row
                    Team team = new Team();
                    team.Day = row.Day;
                    team.Room = emptyRoom;
                    team.Row = row;
                    team.State = TeamState.In;
                    _context.Team.Add(team);
                    await _context.SaveChangesAsync();
                }

                foreach (Player player in PlayerList)
                {
                    player.State = PlayerState.UnClaimed;
                    player.Room = emptyRoom;
                    _context.Player.Add(player);
                    await _context.SaveChangesAsync();
                }

                return RedirectToPage("./Index");
            }
            return RedirectToPage("./Index");
        }
    }
}
