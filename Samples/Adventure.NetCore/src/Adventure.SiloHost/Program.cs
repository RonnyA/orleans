using Orleans;
using System;
using System.IO;
using System.Reflection;
using Orleans.Runtime.Configuration;
using Orleans.Hosting;
using System.Threading.Tasks;
using AdventureGrains;
using Microsoft.Extensions.Logging;

namespace AdventureSetup
{
    class Program
    {
        public static int Main(string[] args)
        {
            return RunMainAsync().Result;
        }

        private static async Task<int> RunMainAsync()
        {
            try
            {
                var host = await StartSilo();
                InitializeGame(host);

                Console.WriteLine("Press Enter to terminate...");
                Console.ReadLine();

                await host.StopAsync();

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static void InitializeGame(ISiloHost host)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string mapFileName = Path.Combine(path, "AdventureMap.json");


            if (!File.Exists(mapFileName))
            {
                Console.WriteLine("*** File not found: {0}", mapFileName);
                return;
            }

            Console.WriteLine("Map file name is '{0}'.", mapFileName);
            Console.WriteLine("Setting up Adventure, please wait ...");

            Adventure adventure = new Adventure();
            adventure.ConnectLocalSilo().Wait();
            adventure.Configure(mapFileName).Wait();
            Console.WriteLine("Adventure setup completed.");



        }

        private static async Task<ISiloHost> StartSilo()
        {
            // define the cluster configuration
            var config = ClusterConfiguration.LocalhostPrimarySilo();
            config.AddMemoryStorageProvider("DefaultProvider");

            var builder = new SiloHostBuilder()
                .UseConfiguration(config)
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(PlayerGrain).Assembly).WithReferences())
                .ConfigureLogging(logging => logging.AddConsole());

            var host = builder.Build();
            await host.StartAsync();
            return host;
        }
    }
}
