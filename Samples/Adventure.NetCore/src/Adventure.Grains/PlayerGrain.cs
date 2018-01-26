using AdventureGrainInterfaces;
using Orleans;
using Orleans.Providers;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureGrains
{

    public class PlayerGrainState
    {
        public IRoomGrain roomGrain; // Current room
        public List<Thing> things = new List<Thing>(); // Things that the player is carrying
        public bool killed = false;

        public PlayerInfo myInfo;
    }

    [StorageProvider(ProviderName = "DefaultProvider")]
    public class PlayerGrain : Orleans.Grain<PlayerGrainState>, IPlayerGrain
    {
        private ObserverSubscriptionManager<IMessage> _subsManager;




        public override Task OnActivateAsync()
        {
            State.myInfo = new PlayerInfo { Key = this.GetPrimaryKey(), Name = "nobody" };


            // Hook up stream where we push messages to Client
            _subsManager = new ObserverSubscriptionManager<IMessage>();

            return base.OnActivateAsync();
        }

        // Clients call this to subscribe.
        public Task Subscribe(IMessage observer)
        {
             _subsManager.Subscribe(observer);
            return Task.CompletedTask;
        }

        //Also clients use this to unsubscribe themselves to no longer receive the messages.
        public Task UnSubscribe(IMessage observer)
        {
            _subsManager.Unsubscribe(observer);
            return Task.CompletedTask;
        }

        public Task SendUpdateMessage(string message)
        {
            _subsManager.Notify(s => s.ReceiveMessage(message));
            return Task.CompletedTask;
        }

        Task<string> IPlayerGrain.Name()
        {
            return Task.FromResult(State.myInfo.Name);
        }

        Task<IRoomGrain> IPlayerGrain.RoomGrain()
        {
            return Task.FromResult(State.roomGrain);
        }


        async Task IPlayerGrain.Die(PlayerInfo killer, Thing weapon)
        {
            await DropAllItems();

            // Exit the game
            if (State.roomGrain != null)
            {
                if (killer != null)
                {
                    // He was killed by a player
                    await State.roomGrain.ExitDead(State.myInfo, killer, weapon);
                    await SendUpdateMessage("You where killed by " + killer.Name + "!");
                }
                else
                {
                    await State.roomGrain.Exit(State.myInfo);
                    await SendUpdateMessage("You died!");
                }
                State.roomGrain = null;
                State.killed = true;

                await base.WriteStateAsync();
            }

        }

        private async Task DropAllItems()
        {
            // Drop everything
            var tasks = new List<Task<string>>();
            foreach (var thing in new List<Thing>(State.things))
            {
                tasks.Add(this.Drop(thing));
            }
            await Task.WhenAll(tasks);

            await base.WriteStateAsync();

        }

        async Task<string> Drop(Thing thing)
        {
            if (State.killed)
                return await CheckAlive();

            if (thing != null)
            {
                State.things.Remove(thing);
                await State.roomGrain.Drop(thing);

                await base.WriteStateAsync();

                return "Okay.";
            }
            else
                return "I don't understand.";
        }

        async Task<string> Take(Thing thing)
        {
            if (State.killed)
                return await CheckAlive();

            if (thing != null)
            {
                State.things.Add(thing);
                await State.roomGrain.Take(thing);
                await base.WriteStateAsync();

                return "Okay.";
            }
            else
                return "I don't understand.";
        }


        Task IPlayerGrain.SetName(string name)
        {
            State.myInfo.Name = name;
            return base.WriteStateAsync(); //Task.CompletedTask;
        }

        async Task IPlayerGrain.SetRoomGrain(IRoomGrain room)
        {
            State.roomGrain = room;
            await room.Enter(State.myInfo);
            await base.WriteStateAsync();
        }

        async Task<string> Go(string direction)
        {
            IRoomGrain destination = await State.roomGrain.ExitTo(direction);

            StringBuilder description = new StringBuilder();

            if (destination != null)
            {
                await State.roomGrain.Exit(State.myInfo);
                await destination.Enter(State.myInfo);
                State.roomGrain = destination;

                await base.WriteStateAsync();

                var desc = await destination.Description(State.myInfo);

                if (desc != null)
                    description.Append(desc);
            }
            else
            {
                description.Append("You cannot go in that direction.");
            }

            if (State.things.Count > 0)
            {
                description.AppendLine("You are holding the following items:");
                foreach (var thing in State.things)
                {
                    description.AppendLine(thing.Name);
                }
            }

            return description.ToString();
        }

        async Task<string> CheckAlive()
        {
            if (!State.killed)
                return null;

            // Go to room '-2', which is the place of no return.
            var room = GrainFactory.GetGrain<IRoomGrain>(-2);
            return await room.Description(State.myInfo);
        }

        async Task<string> Kill(string target)
        {
            if (State.things.Count == 0)
                return "With what? Your bare hands?";

            var player = await State.roomGrain.FindPlayer(target);
            if (player != null)
            {
                if (player.Key  == State.myInfo.Key)
                {
                    return "You can't kill yourself!";                
                }

                var weapon = State.things.Where(t => t.Category == "weapon").FirstOrDefault();
                if (weapon != null)
                {
                    await GrainFactory.GetGrain<IPlayerGrain>(player.Key).Die(State.myInfo, weapon);
                    return target + " is now dead.";                    
                }
                return "With what? Your bare hands?";
            }

            var monster = await State.roomGrain.FindMonster(target);
            if (monster != null)
            {
                var weapons = monster.KilledBy.Join(State.things, id => id, t => t.Id, (id, t) => t);
                if (weapons.Count() > 0)
                {
                    Thing weapon = weapons.Where(t => t.Category == "weapon").FirstOrDefault();

                    await GrainFactory.GetGrain<IMonsterGrain>(monster.Id).Kill(State.roomGrain, player, weapon);
                    return target + " is now dead.";
                }
                return "With what? Your bare hands?";
            }
            return "I can't see " + target + " here. Are you sure?";
        }

        private string RemoveStopWords(string s)
        {
            string[] stopwords = new string[] { " on ", " the ", " a " };

            StringBuilder sb = new StringBuilder(s);
            foreach (string word in stopwords)
            {
                sb.Replace(word, " ");
            }

            return sb.ToString();
        }

        private Thing FindMyThing(string name)
        {
            return State.things.Where(x => x.Name == name).FirstOrDefault();
        }

        private string Rest(string[] words)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 1; i < words.Length; i++)
                sb.Append(words[i] + " ");

            return sb.ToString().Trim().ToLower();
        }

        async Task IPlayerGrain.SendMessage(string message)
        {
            await SendUpdateMessage(message);
        }

        async Task<string> Whisper(string words)
        {
            if (State.roomGrain != null)
            {
                await State.roomGrain.Whisper(words, State.myInfo);
            }
            return "You whispered '" + words + "'";
        }

        async Task<string> Shout(string words)
        {
            //TODO: Find a way to tell everyone in all rooms
            if (State.roomGrain != null)
            {
                await State.roomGrain.Shout(words, State.myInfo);
            }
            
            return "You shouted '" + words + "'";
        }

        async Task<string> Leave()
        {
            await DropAllItems();

            if (State.roomGrain != null)
            {
                await State.roomGrain.Leave(State.myInfo);
            }            

            return "You left the game";           
        }

        async Task<string> IPlayerGrain.Play(string command)
        {
            Thing thing;
            command = RemoveStopWords(command);

            string[] words = command.Split(' ');

            string verb = words[0].ToLower();

            if (State.killed && verb != "end")
                return await CheckAlive();


            verb = map_shortcuts(verb);

           
            switch (verb)
            {
                case "l":
                case "look":
                    return await State.roomGrain.Description(State.myInfo);

                case "go":
                    if (words.Length == 1)
                        return "Go where?";
                    return await Go(words[1]);

                case "north":
                case "south":
                case "east":
                case "west":
                    return await Go(verb);

                case "kill":
                    if (words.Length == 1)
                        return "Kill what?";
                    var target = command.Substring(verb.Length + 1);
                    return await Kill(target);

                case "drop":
                    thing = FindMyThing(Rest(words));
                    return await Drop(thing);

                case "take":
                    thing = await State.roomGrain.FindThing(Rest(words));
                    return await Take(thing);

                case "i":
                case "inv":
                case "inventory":
                    return "You are carrying: " + string.Join(" ", State.things.Select(x => x.Name));

                case "shout":
                    return await Shout(Rest(words));

                case "whisper":
                    return await Whisper(Rest(words));

                case "help":
                    return "Available commands: Look, North, South, East, West, Kill, Drop, Take, Inventory, Shout, Whisper, End";
                case "end":
                    await Leave();
                    return "";
            }
            return "I don't understand.";
        }

        private string map_shortcuts(string verb)
        {
            // Map shortcuts
            switch (verb)
            {
                case "n":
                    verb = "north";
                    break;
                case "s":
                    verb = "south";
                    break;
                case "e":
                    verb = "east";
                    break;
                case "w":
                    verb = "west";
                    break;
            }

            return verb;
        }
    }
}

