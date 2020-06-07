using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using DOTW.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DOTW
{
    public class PlayModel : PageModel
    {
        private readonly DOTW.Data.DotwDbContext _context;

        public PlayModel(DOTW.Data.DotwDbContext context)
        {
            _context = context;
        }

        public Room Room { get; set; }
        public Player Player { get; set; }
        public List<Player> Players { get; set; }
        public bool PlayerConfirmed { get; set; }
        public bool PlayerCanMove { get; set; }
        public string UserMessage { get; set; }
        public List<Player> MyTeamMates { get; set; }
        public Team MyTeam { get; set; }

        public async Task<IActionResult> OnGetAsync(int? RoomId, int? PlayerId, string UserMessage)
        {
            this.UserMessage = UserMessage;
            // Let RoomID override if it is specified. 
            if (RoomId == null)
            {
                return NotFound();
            }
            PlayerConfirmed = false;
            if (PlayerId != null)
            {
                Player = await _context.Player.FindAsync(PlayerId);
                if (Player != null)
                {
                    PlayerConfirmed = true;
                }
            }
            // This isn't terribly effecient case we end up with players included twice 
            // but it does reflect the room. 
            Room = await _context.Room
                  .Include(R => R.Rows)
                    .ThenInclude(row => row.Chairs)
                    .ThenInclude(chair => chair.Player)
                  .Where(R => R.ID == RoomId).FirstAsync();

            if (Room == null)
            {
                return NotFound();
            }

            Players = await _context.Player.Where(p => p.RoomId == Room.ID)
                   .Include(R => R.Team)
                   .OrderBy(p => p.Name)
                   .ToListAsync();

            if (PlayerConfirmed && Player.Team != null)
            {
                MyTeamMates = Players.Where(p => p.Team == Player.Team).ToList();
                MyTeam = Player.Team;
            }

            if (Room.State != RoomState.Closed)
            {
                ViewData["AutoRefresh"] = "Yes";
            }
            ViewData["PlayerOptions"] = "Yes";
            PlayerCanMove = false; 
            if (PlayerConfirmed)
            {
                if (Player.State == PlayerState.InPlay)
                {
                    PlayerCanMove = true;
                }
            }
            return Page();
        }

        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostClaimUserAsync(int RoomId, int PlayerId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "That's Odd! Model was invalid" });
            }

            Player = await _context.Player.FindAsync(PlayerId);
            Player.State = PlayerState.InPlay;
            _context.Attach(Player).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // we'll return to Play without a PlayerID, to claim another.
                return RedirectToPage("Play", new { RoomId = RoomId, UserMessage = "Thats Odd! We were unable to claim that user." });
            }

            return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Welcome to the game! Now take a seat, the row you choose becomes your Team." });
        }

        public async Task<IActionResult> OnPostClaimSeatAsync(int RoomId, int PlayerId, int ChairId)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "That's Odd! Model was invalid." });
            }

            // Check the Player out. 
            try
            {
                Player = await _context.Player.Include(p => p.Team)
                                .ThenInclude(team => team.Players)
                                .Where(p => p.ID == PlayerId).FirstAsync();
            }
            catch { return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "That's Odd! We couldn't find you as a player, ask the host to release you so you can re-claim." }); }
            if (Player.State == PlayerState.ProperlySeated) { return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Sorry, you can't move once you are seated in the right row" }); }
            if (Player.State != PlayerState.InPlay) { return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Sorry, you can't change seats, you are no longer in play." }); }

            // Vacate the previous chair first. 
            Chair ChairVacated = new Chair();
            try
            {
                ChairVacated = await _context.Chair.Where(c => c.Player.ID == Player.ID).FirstAsync();
            }
            catch { } // we don't really care if we can't find ChairVacated.
            // First thing we'll do is vacate our previous chair, as there is a race condition here where somebody may grab the chair, while another is giving it up. 
            if (ChairVacated.Player == Player)
            {
                //if (ChairVacated.State == ChairState.OccupiedInvalid)
                //{
                    ChairVacated.State = ChairState.Empty;
                    ChairVacated.Player = null;
                    _context.Attach(ChairVacated).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                //}
            }

            // Now let's deal with the new chair.
            Chair Chair = await _context.Chair
                                .Include(c => c.Row)
                                    .ThenInclude(row => row.Chairs)
                                .Where(c => c.ID == ChairId).FirstAsync();
            Room = await _context.Room.FindAsync(RoomId);
            if (Chair == null)
            {
                return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "That's Odd! Couldn't find that chair." });
            }
            if (Chair.State == ChairState.OccupiedValid) // can't have this one your teammate got it.
            {
                return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Sorry, that chair was taken while you weren't looking." });
            }
            // Make sure the room is not closed. 
            if (Room.State == RoomState.Closed) { return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Sorry, you can't sit down now this room is closed!" }); }
            // Now let's setup our changes.
            Chair.Player = Player;
            if (Room.State == RoomState.TeamSelection)
            {
                Chair.State = ChairState.OccupiedValid;
            }
            else
            {
                // Player isn't on a team yet, but the room is not in TeamSelection. 
                if (Player.Team == null) { return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Sorry, You can not sit down while the room is in play, ask the Host to open the room for Team Selection." }); }
                if (Player.Team.State == TeamState.Out) { return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Sorry, your team is out, you can't sit down." }); }
                // is the Player in the right Row? 
                if (Player.Team.Day == Chair.Row.Day)
                {
                    Chair.State = ChairState.OccupiedValid;
                    // Is the row complete now? 
                    int PlayerCount = Player.Team.Players.Count;
                    int OccupiedCharCount = Chair.Row.Chairs.Where(c => c.State == ChairState.OccupiedValid).ToList().Count;
                    if (OccupiedCharCount == PlayerCount)
                    {
                        Row CompletedRow = Chair.Row;
                        CompletedRow.CompletedTime = DateTime.Now;
                        CompletedRow.State = RowState.RowComplete;
                        CompletedRow.TimeToComplete = CompletedRow.CompletedTime - Room.LastDayChange;
                        _context.Attach(CompletedRow).State = EntityState.Modified;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    Chair.State = ChairState.OccupiedInvalid;
                }
            }
            // Try to update the chair now. 
            _context.Attach(Chair).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "Sorry, that chair was taken while you weren't looking!" });
            }
            // Now let's see if we need to update our player. 
            // Mark the player as properly seated so they don't get up and move around. 
            if (Room.State == RoomState.InPlay && Chair.State == ChairState.OccupiedValid)
            {
                Player.State = PlayerState.ProperlySeated;
                _context.Attach(Player).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }

            if (Room.State == RoomState.TeamSelection)
            {
                Team Team = await _context.Team.Where(t => t.RoomId == Room.ID && t.Day == Chair.Row.Day).FirstOrDefaultAsync();
                Player.Team = Team;
                _context.Attach(Player).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            
            return RedirectToPage("Play", new { RoomId = RoomId, PlayerId = PlayerId, UserMessage = "That seat is yours!"});
        }
    }
}