using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using DOTW.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace DOTW
{
    [Authorize]
    public class HostModel : PageModel
    {

        private readonly UserManager<IdentityUser> _userManager;
        private readonly DOTW.Data.DotwDbContext _context;

        public HostModel(UserManager<IdentityUser> userManager, DOTW.Data.DotwDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public Room Room { get; set; }

        public List<Player> Players { get; set; }
        public List<Team> Teams { get; set; }
        public string UserMessage { get; set;  }

        public List<SelectListItem> DaySelectList { get; set; }

        public async Task<IActionResult> OnGetAsync(int? RoomId, string UserMessage)
        {
            this.UserMessage = UserMessage;
            // Let RoomID override if it is specified. 
            if (RoomId == null)
            {
                return NotFound();
            }
            // This isn't terribly effecient case we end up with players included twice 
            // but it does reflect the room. 
            Room = await _context.Room
                  .Include(R => R.Rows)
                    .ThenInclude(row => row.Chairs)
                    .ThenInclude(chair => chair.Player)
                  .Where(R => R.ID == RoomId).FirstAsync();

            if(Room == null)
            {
                return NotFound();
            }

            // Get our logged in user: 
            var user = await _userManager.GetUserAsync(User);
            if (Room.HostEmail.ToLower() != user.Email.ToLower())
            {
                return RedirectToPage("Error", new { Error = "Sorry! Only the authorized Host may host a room" });
            }

            Teams = await _context.Team.Where(T => T.Room == Room).ToListAsync();
            Players = await _context.Player.Where(p => p.RoomId == Room.ID)
                                           .Include(R => R.Team)
                                           .OrderBy(p => p.Name)
                                           .ToListAsync();
            // Set TempTeamId if there is one
            foreach (Row row in Room.Rows)
            {
                try { 
                    Team team = Teams.Where(t => t.Row == row).First();
                    row.TempTeamId = team.ID;
                }
                catch { row.TempTeamId = 0; continue; }
            }

            DaySelectList =  Enum.GetValues(typeof(Day)).Cast<Day>().Select(v => new SelectListItem
            {
                Text = v.ToString(),
                Value = ((int)v).ToString()
            }).ToList();

            if (Room.State == RoomState.InPlay)
            {
                ViewData["AutoRefresh"] = "Yes";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostChangeDayAsync(int? RoomId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! Model was invalid" });
            }
            
            Room roomToUpdate = await _context.Room
                                        .Include(r => r.Rows)
                                            .ThenInclude(Rows => Rows.Chairs)
                                            .ThenInclude(Chairs => Chairs.Player)
                                        .Where(r => r.ID == RoomId).FirstAsync();

            if (await TryUpdateModelAsync<Room>(
                roomToUpdate,
                "Room",
                r => r.Day))
            {
                // Update all of the Rows and Users before we update room to InPlay
                foreach (Row row in roomToUpdate.Rows)
                {
                    int NewDay = row.Position + ((int)roomToUpdate.Day -1);
                    if (NewDay > 7) { NewDay = NewDay - 7; }
                    row.Day = (Day)NewDay;
                    row.State = RowState.InPlay;
                    _context.Attach(row).State = EntityState.Modified;
                    foreach (Chair chair in row.Chairs)
                    {
                        // invalidate everybodies seats. 
                        if (chair.State != ChairState.Empty)
                        {
                            chair.State = ChairState.OccupiedInvalid;
                            if (chair.Player.State == PlayerState.ProperlySeated)
                            {
                                chair.Player.State = PlayerState.InPlay;
                            }
                            _context.Attach(chair).State = EntityState.Modified;
                        }
                    }
                    await _context.SaveChangesAsync();
                    // find the associated team for this row. 
                    Team team = await _context.Team.Where(t => t.RoomId == RoomId && t.Day == row.Day).FirstAsync();
                    team.Row = row;
                    _context.Attach(team).State = EntityState.Modified;
                    // save one rows worth of changes at a time. 
                    await _context.SaveChangesAsync();
                }
                roomToUpdate.State = RoomState.InPlay;
                roomToUpdate.LastDayChange = DateTime.Now;
                await _context.SaveChangesAsync();
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "Day Change Successful!" });
            }
            return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We didn't complete that room change" });
        }
        public async Task<IActionResult> OnPostReleasePlayerAsync(int? RoomId, int PlayerId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! Model was invalid" });
            }

            Player player = await _context.Player.FindAsync(PlayerId);
            if (player == null)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We weren't able to find that player?" });
            }

            player.State = PlayerState.UnClaimed;
            _context.Attach(player).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "Player: " + player.Name + " ready to be claimed."  });
        }

        public async Task<IActionResult> OnPostRenamePlayerAsync(int? RoomId, int PlayerId, string NewName)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! Model was invalid" });
            }

            Player player = await _context.Player.FindAsync(PlayerId);
            if (player == null)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We weren't able to find that player?" });
            }

            player.Name = NewName;
            _context.Attach(player).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "Player renamed to: " + player.Name });
        }

        public async Task<IActionResult> OnPostTeamSelectionAsync(int? RoomId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! Model was invalid" });
            }

            Room room = await _context.Room.FindAsync(RoomId);
            if (room == null)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We weren't able to find that room?" });
            }

            room.State = RoomState.TeamSelection;
            room.Day = Day.Sunday;
            _context.Attach(room).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            // Reset each Row's day as well 
            await _context.Entry(room)
                .Collection(r => r.Rows)
                .LoadAsync();
            foreach (Row row in room.Rows)
            {
                row.State = RowState.AcceptingPlayers;
                row.Day = (Day)row.Position;
                _context.Attach(row).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            // reset each team... no more outs. 
            Teams = await _context.Team.Where(T => T.Room == room).ToListAsync();
            foreach (Team team in Teams)
            {
                team.State = TeamState.In;
                _context.Attach(team).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "This room is open for Team selection" });
        }
        public async Task<IActionResult> OnPostCloseRoomAsync(int? RoomId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! Model was invalid" });
            }

            Room room = await _context.Room.FindAsync(RoomId);
            if (room == null)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We weren't able to find that room?" });
            }

            room.State = RoomState.Closed;
            room.Day = Day.Sunday;
            _context.Attach(room).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            // Reset each Row's day as well 
            await _context.Entry(room)
                .Collection(r => r.Rows)
                .LoadAsync();
            foreach (Row row in room.Rows)
            {
                row.State = RowState.AcceptingPlayers;
                row.Day = (Day)row.Position;
                _context.Attach(row).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "This room is now closed" });
        }

        public async Task<IActionResult> OnPostTeamOutAsync(int? RoomId, int TeamId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! Model was invalid" });
            }

            Room room = await _context.Room.FindAsync(RoomId);
            if (room == null)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We weren't able to find that room?" });
            }

            Team team = await _context.Team.FindAsync(TeamId);
            if (team == null)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We weren't able to find that team?" });
            }

            team.State = TeamState.Out;
            _context.Attach(team).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            //Now we gotta mark each players out as well. 
            await _context.Entry(team)
                .Collection(t => t.Players)
                .LoadAsync();
            foreach (Player player in team.Players)
            {
                player.State = PlayerState.Out;
                _context.Attach(player).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "Team " + team.Day + " is Out" });
        }

        public async Task<IActionResult> OnPostResetRoomAsync(int RoomId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! Model was invalid" });
            }

            try
            {
                Room = await _context.Room
                                  .Include(R => R.Players)
                                  .Include(R => R.Rows)
                                    .ThenInclude(row => row.Chairs)
                                  .Where(R => R.ID == RoomId).FirstAsync();
            }
            catch
            {
                return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "That's Odd! We weren't able to find that room?" });
            }

            // Get our logged in user: 
            var user = await _userManager.GetUserAsync(User);
            if (Room.HostEmail.ToLower() != user.Email.ToLower())
            {
                return RedirectToPage("Error", new { Error = "Sorry! Only the Host may reset a room" });
            }

            // Start by resetting our Room to defaults. 
            Room.State = RoomState.Closed;
            Room.Day = Day.Sunday;
            _context.Attach(Room).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Now let's reset our Rows, Chairs, Teams and Players. 
            foreach (Row row in Room.Rows)
            {
                row.Day = (Day)row.Position;
                row.State = RowState.AcceptingPlayers;
                _context.Attach(row).State = EntityState.Modified;
                foreach (Chair chair in row.Chairs)
                {
                    chair.State = ChairState.Empty;
                    chair.Player = null;
                    _context.Attach(chair).State = EntityState.Modified;
                }
                await _context.SaveChangesAsync();
            }
            // We're going to fetch all teams rather than rely on nav properties. 
            List<Team> teams = await _context.Team.Where(t => t.RoomId == Room.ID).ToListAsync();
            foreach (Team team in teams)
            {
                team.State = TeamState.In;
                _context.Attach(team).State = EntityState.Modified;
            }
            await _context.SaveChangesAsync();
            foreach (Player player in Room.Players)
            {
                if (player.State == PlayerState.ProperlySeated || player.State == PlayerState.Out)
                {
                    player.State = PlayerState.InPlay;
                    _context.Attach(player).State = EntityState.Modified;
                }
            }
            await _context.SaveChangesAsync();

            return RedirectToPage("Host", new { RoomId = RoomId, UserMessage = "This Room has been Reset" });
        }
    }
}