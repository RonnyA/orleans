using AdventureGrainInterfaces;
using Orleans;
using System;
using Orleans.Runtime.Configuration;
using Orleans.Streams;
using System.Threading.Tasks;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;

namespace AdventureClient
{
   
    class Program
    {

        static int Main(string[] args)
        {
            return RunMainAsync().Result;
        }

        private static async Task<int> RunMainAsync()
        {
            try
            {
                using (var client = await StartClientWithRetries())
                {
                    DoClientWork(client);                                        
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }
        }

        private static async Task<IClusterClient> StartClientWithRetries(int initializeAttemptsBeforeFailing = 5)
        {
            int attempt = 0;
            IClusterClient client;
            while (true)
            {
                try
                {
                    var config = ClientConfiguration.LocalhostSilo();
                    client = new ClientBuilder()
                        .UseConfiguration(config)
                        .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(IPlayerGrain).Assembly).WithReferences())
                        .ConfigureLogging(logging => logging.AddConsole())
                        .Build();

                    await client.Connect();
                    Console.WriteLine("Client successfully connect to silo host");
                    break;
                }
                catch (SiloUnavailableException)
                {
                    attempt++;
                    Console.WriteLine($"Attempt {attempt} of {initializeAttemptsBeforeFailing} failed to initialize the Orleans client.");
                    if (attempt > initializeAttemptsBeforeFailing)
                    {
                        throw;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(4));
                }
            }

            return client;
        }

        //static void Main(string[] args)
        //{
        //    var config = ClientConfiguration.LocalhostSilo();

        //    GrainClient.Initialize(config);

        //    MainAsync(args).Wait(-1); // Wait for ever, never timeout
        //}


        private static void DoClientWork(IClusterClient client)
        {
       

            Console.WriteLine(@"
  ___      _                 _                  
 / _ \    | |               | |                 
/ /_\ \ __| |_   _____ _ __ | |_ _   _ _ __ ___ 
|  _  |/ _` \ \ / / _ \ '_ \| __| | | | '__/ _ \
| | | | (_| |\ V /  __/ | | | |_| |_| | | |  __/
\_| |_/\__,_| \_/ \___|_| |_|\__|\__,_|_|  \___|");

            Console.WriteLine();
            Console.WriteLine("What's you name?");
            string name = Console.ReadLine();
            Guid playerGuid = Guid.NewGuid();

            var player = client.GetGrain<IPlayerGrain>(playerGuid);
            player.SetName(name).Wait();            

            // Subscribe to messages from the server
            Message m = new Message();
            var obj = client.CreateObjectReference<IMessage>(m).Result;
            player.Subscribe(obj).Wait();


            var room1 = client.GetGrain<IRoomGrain>(0);            
            player.SetRoomGrain(room1).Wait();

           
            Console.WriteLine(player.Play("look").Result);

            string result = "Start";

            try
            {
                while (result != "")
                {


                    string command = Console.ReadLine();

                    result = player.Play(command).Result;
                    Console.WriteLine(result);
                }
            }
            finally
            {
                player.Die(null, null).Wait();
                Console.WriteLine("Game over!");
            }
        }

     
        public class Message : IGrainObserver,IMessage
        {
            public void ReceiveMessage(string message)
            {
                Console.WriteLine(message);
            }
        }        
    }
}
