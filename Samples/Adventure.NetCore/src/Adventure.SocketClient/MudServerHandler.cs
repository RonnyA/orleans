using System;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Orleans;
using AdventureGrainInterfaces;

namespace AdventureSocketClient
{

    public class MudServerHandler : ChannelHandlerAdapter
    {

        bool waitingForName = true;
        IPlayerGrain _player = null;
        Message _message = null;
        IMessage _messageInterface = null;
        private IClusterClient _client;

        public MudServerHandler(IClusterClient client)
        {
            this._client = client;
        }

        public override void ChannelActive(IChannelHandlerContext contex)
        {
            contex.WriteAsync(@"
  ___      _                 _                  
 / _ \    | |               | |                 
/ /_\ \ __| |_   _____ _ __ | |_ _   _ _ __ ___ 
|  _  |/ _` \ \ / / _ \ '_ \| __| | | | '__/ _ \
| | | | (_| |\ V /  __/ | | | |_| |_| | | |  __/
\_| |_/\__,_| \_/ \___|_| |_|\__|\__,_|_|  \___|");

            contex.WriteAsync(string.Format("\r\nWelcome to {0} !\r\n", Dns.GetHostName()));
            contex.WriteAsync(string.Format("It is {0} now !\r\n\r\n", DateTime.Now));

            contex.WriteAndFlushAsync("What's you name?");

        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            try
            {
                string msg = (string)message;
                // Generate and write a response.
                this.ProcessMessage(context, msg);
            }
            catch (Exception ex)
            {

                Console.WriteLine("Exception in ChannelRead0: " + ex);
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            killPlayer();
        }

        private async void killPlayer()
        {
            if (this._player != null)
            {
                await this._player.Play("End"); // disconnect
                await this._player.UnSubscribe(this._messageInterface); // Unsubscribe - Observers pattern
            }

            await this._client.DeleteObjectReference<IMessage>(this._messageInterface);            

            this._message = null;
            this._messageInterface = null;
            this._player = null;

        }



        private async void ProcessMessage(IChannelHandlerContext context, string msg)
        {
            string response = "";
            bool close = false;

            try
            {

                if (string.IsNullOrEmpty(msg))
                {
                    response = "Please type something.\r\n";
                }
                else if (this.waitingForName)
                {
                    this.waitingForName = false;

                    // Create player
                    Guid playerGuid = Guid.NewGuid();

                    this._player = this._client.GetGrain<IPlayerGrain>(playerGuid);

                    await this._player.SetName(msg);


                    // Implement Observers pattern - https://dotnet.github.io/orleans/Documentation/Getting-Started-With-Orleans/Observers.html
                    this._message = new Message(context);

                    //Create a reference for Message usable for subscribing to the observable grain.
                    this._messageInterface = this._client.CreateObjectReference<IMessage>(this._message).Result;

                    await this._player.Subscribe(this._messageInterface);

                    // Put the player into the default Room
                    var room1 = this._client.GetGrain<IRoomGrain>(0);
                    await this._player.SetRoomGrain(room1);

                    response = await this._player.Play("look");

                }
                else if (string.Equals("End", msg, StringComparison.OrdinalIgnoreCase))
                {
                    response = "Have a good day!\r\n";

                    close = true;
                }
                else
                {
                    response = await this._player.Play(msg);
                }
            }
            catch (Exception ex)
            {
                response = "Unexpected exception: " + ex;
                close = true;
            }
            
            await context.WriteAndFlushAsync(response + "\r\n");

            if (close)
            {                
                await context.CloseAsync();
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext contex)
        {
            contex.Flush();
        }

        public async override void ExceptionCaught(IChannelHandlerContext contex, Exception e)
        {
            Console.WriteLine("{0}", e.StackTrace);
            await contex.CloseAsync();
        }

        public override bool IsSharable => true;
    }

    public class Message : IMessage
    {
        IChannelHandlerContext _context;
        public Message(IChannelHandlerContext context)
        {
            this._context = context;
        }
        public void ReceiveMessage(string message)
        {
            this._context.WriteAndFlushAsync(message + "\r\n");
        }
    }

}