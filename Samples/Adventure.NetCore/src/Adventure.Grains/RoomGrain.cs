using AdventureGrainInterfaces;
using Orleans;
using Orleans.Providers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureGrains
{
    public class RoomGrainState
    {
        public string description;

        public List<PlayerInfo> players = new List<PlayerInfo>();
        public List<MonsterInfo> monsters = new List<MonsterInfo>();
        public List<Thing> things = new List<Thing>();

        public Dictionary<string, IRoomGrain> exits = new Dictionary<string, IRoomGrain>();
    }
    /// <summary>
    /// Orleans grain implementation class Grain1.
    /// </summary>
    [StorageProvider(ProviderName = "DefaultProvider")]
    public class RoomGrain : Orleans.Grain<RoomGrainState>, IRoomGrain
    {
        // TODO: replace placeholder grain interface with actual grain
        // communication interface(s).



        // Send a text message to all players in the room               
        async void SendMessageToAllPlayersInRoomExceptPlayer(string message, PlayerInfo exPlayer)
        {
            System.Guid exID = System.Guid.Empty;
            if (exPlayer != null)
            {
                exID = exPlayer.Key;
            }

            foreach(PlayerInfo p in State.players)
            {
                if (exID != p.Key)
                {
                    var player = GrainFactory.GetGrain<IPlayerGrain>(p.Key);
                    await player.SendMessage(message);
                }
            }            
        }

        Task IRoomGrain.Whisper(string words, PlayerInfo sender)
        {
            string message = sender.Name + " whispers '" + words + "'";
            SendMessageToAllPlayersInRoomExceptPlayer(message, sender);

            return Task.CompletedTask;
        }

        Task IRoomGrain.Shout(string words, PlayerInfo sender)
        {
            string message = sender.Name + " SHOUTS '" +  words.ToUpper() + "'";
            SendMessageToAllPlayersInRoomExceptPlayer(message, sender);

            return Task.CompletedTask;
        }
        Task IRoomGrain.Enter(PlayerInfo player)
        {
            State.players.RemoveAll(x => x.Key == player.Key);
            State.players.Add(player);


            SendMessageToAllPlayersInRoomExceptPlayer(player.Name + " entered the room.", player);



            return base.WriteStateAsync(); // Task.CompletedTask;
        }

        Task IRoomGrain.Leave(PlayerInfo player)
        {
            State.players.RemoveAll(x => x.Key == player.Key);

            SendMessageToAllPlayersInRoomExceptPlayer(player.Name + " left the game.", player);

            return base.WriteStateAsync(); //Task.CompletedTask;
        }

        Task IRoomGrain.Exit(PlayerInfo player)
        {
            State.players.RemoveAll(x => x.Key == player.Key);

            SendMessageToAllPlayersInRoomExceptPlayer(player.Name + " left the room.", player);

            return base.WriteStateAsync(); //Task.CompletedTask;
        }

        Task IRoomGrain.ExitDead(PlayerInfo player, PlayerInfo killer, Thing weapon)
        {
            State.players.RemoveAll(x => x.Key == player.Key);

            string message = player.Name;
            if (killer != null)
            {
                message += " was killed by " + killer.Name;
                if (weapon != null)
                {
                    message += " using a " + weapon.Name;
                }
            }
            else
            {
                if (weapon != null)
                {
                    message += " was killed with a " + weapon.Name;
                }
            }

            SendMessageToAllPlayersInRoomExceptPlayer(message, player);

            return base.WriteStateAsync(); //Task.CompletedTask;

        }


        Task IRoomGrain.Enter(MonsterInfo monster)
        {
            State.monsters.RemoveAll(x => x.Id == monster.Id);
            State.monsters.Add(monster);

            SendMessageToAllPlayersInRoomExceptPlayer(monster.Name + " entered the room.", null);

            
            return base.WriteStateAsync(); //Task.CompletedTask;
        }

        // Monster was killed by someone
        Task IRoomGrain.ExitDead(MonsterInfo monster, PlayerInfo killer, Thing weapon)
        {
            State.monsters.RemoveAll(x => x.Id == monster.Id);

            string message = monster.Name;
            if (killer != null)
            {
                message += " was killed by " + killer.Name;
                if (weapon != null)
                {
                    message += " using a " + weapon.Name;
                }
            }
            else
            {
                if (weapon != null)
                {
                    message += " was killed with a " + weapon.Name;
                }
            }

            SendMessageToAllPlayersInRoomExceptPlayer(message, null);

            return base.WriteStateAsync(); //Task.CompletedTask;

        }
        Task IRoomGrain.Exit(MonsterInfo monster)
        {
            State.monsters.RemoveAll(x => x.Id == monster.Id);

            SendMessageToAllPlayersInRoomExceptPlayer(monster.Name + " left the room.", null);

            
            return base.WriteStateAsync(); //Task.CompletedTask;
        }

        Task IRoomGrain.Drop(Thing thing)
        {
            State.things.RemoveAll(x => x.Id == thing.Id);
            State.things.Add(thing);
            return base.WriteStateAsync(); //Task.CompletedTask;
        }

        Task IRoomGrain.Take(Thing thing)
        {
            State.things.RemoveAll(x => x.Name == thing.Name);
            return base.WriteStateAsync();  //Task.CompletedTask;
        }

        Task IRoomGrain.SetInfo(RoomInfo info)
        {
            State.description = info.Description;

            foreach (var kv in info.Directions)
            {
                State.exits[kv.Key] = GrainFactory.GetGrain<IRoomGrain>(kv.Value);
            }
            return base.WriteStateAsync();  //Task.CompletedTask;
        }

        Task<Thing> IRoomGrain.FindThing(string name)
        {
            return Task.FromResult(State.things.Where(x => x.Name == name).FirstOrDefault());
        }

        Task<PlayerInfo> IRoomGrain.FindPlayer(string name)
        {
            name = name.ToLower();
            return Task.FromResult(State.players.Where(x => x.Name.ToLower().Contains(name)).FirstOrDefault());
        }

        Task<MonsterInfo> IRoomGrain.FindMonster(string name)
        {
            name = name.ToLower();
            return Task.FromResult(State.monsters.Where(x => x.Name.ToLower().Contains(name)).FirstOrDefault());
        }

        Task<string> IRoomGrain.Description(PlayerInfo whoisAsking)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(State.description);

            if (State.things.Count > 0)
            {
                sb.AppendLine("The following things are present:");
                foreach (var thing in State.things)
                {
                    sb.Append("  ").AppendLine(thing.Name);
                }
            }

            var others = State.players.Where(pi => pi.Key != whoisAsking.Key).ToArray();

            if (others.Length > 0 || State.monsters.Count > 0)
            {
                sb.AppendLine("Beware! These guys are in the room with you:");
                if (others.Length > 0)
                    foreach (var player in others)
                    {
                        sb.Append("  ").AppendLine(player.Name);
                    }
                if (State.monsters.Count > 0)
                    foreach (var monster in State.monsters)
                    {
                        sb.Append("  ").AppendLine(monster.Name);
                    }
            }

            return Task.FromResult(sb.ToString());
        }

        Task<IRoomGrain> IRoomGrain.ExitTo(string direction)
        {
            return Task.FromResult((State.exits.ContainsKey(direction)) ? State.exits[direction] : null);
        }
    }
}
