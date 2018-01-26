using AdventureGrainInterfaces;
using Orleans;
using Orleans.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureGrains
{

    public class MonsterGrainState
    {
        public MonsterInfo monsterInfo = new MonsterInfo();
        public IRoomGrain roomGrain; // Current room
    }

    [StorageProvider(ProviderName = "DefaultProvider")]
    public class MonsterGrain : Orleans.Grain<MonsterGrainState>, IMonsterGrain
    {

        public override Task OnActivateAsync()
        {
            State.monsterInfo.Id = this.GetPrimaryKeyLong();

            RegisterTimer((_) => Move(), null, TimeSpan.FromSeconds(150), TimeSpan.FromMinutes(150));
            return base.OnActivateAsync();
        }

        Task IMonsterGrain.SetInfo(MonsterInfo info)
        {
            State.monsterInfo = info;
            return base.WriteStateAsync(); // Task.CompletedTask;
        }

        Task<string> IMonsterGrain.Name()
        {
            return Task.FromResult(State.monsterInfo.Name);
        }

        async Task IMonsterGrain.SetRoomGrain(IRoomGrain room)
        {
            if (State.roomGrain != null)
                await State.roomGrain.Exit(State.monsterInfo);
            State.roomGrain = room;
            await base.WriteStateAsync();

            await State.roomGrain.Enter(State.monsterInfo);
        }

        Task<IRoomGrain> IMonsterGrain.RoomGrain()
        {
            return Task.FromResult(State.roomGrain);
        }

        async Task Move()
        {
            var directions = new string [] { "north", "south", "west", "east" };

            var rand = new Random().Next(0, 4);
            IRoomGrain nextRoom = await State.roomGrain.ExitTo(directions[rand]);

            if (null == nextRoom) 
                return;

            await State.roomGrain.Exit(State.monsterInfo);
            await nextRoom.Enter(State.monsterInfo);

            State.roomGrain = nextRoom;
            await base.WriteStateAsync();
        }


        Task<string> IMonsterGrain.Kill(IRoomGrain room, PlayerInfo killer, Thing weapon)
        {
            if (State.roomGrain != null)
            {
                if (State.roomGrain.GetPrimaryKey() != room.GetPrimaryKey())
                {
                    return Task.FromResult(State.monsterInfo.Name + " snuck away. You were too slow!");
                }
                return State.roomGrain.ExitDead(State.monsterInfo, killer, weapon).ContinueWith(t => State.monsterInfo.Name + " is dead.");
            }
            return Task.FromResult(State.monsterInfo.Name + " is already dead. You were too slow and someone else got to him!");
        }
    }
}
