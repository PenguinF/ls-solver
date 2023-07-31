using Eutherion.Text.Json;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebsocket;

namespace FPuzzlesServer
{
    class Program
    {
        const int FPuzzlesPort = 4545;

        private readonly static WatsonWsServer server = new("localhost", FPuzzlesPort);

        public static async Task Main()
        {
            try
            {
                using (server)
                {
                    server.ClientConnected += (_, args) => ClientConnected(args);
                    server.ClientDisconnected += (_, args) => ClientDisconnected(args);
                    server.MessageReceived += (_, args) => MessageReceived(args);
                    await server.StartAsync();

                    Console.WriteLine("Server initialized. Press Q to quit.");
                    Console.WriteLine();

                    while (true)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q)
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine($"{exc.GetType().FullName}: {exc.Message}");
                Console.Write(exc.StackTrace);
            }
        }

        static void ClientConnected(ClientConnectedEventArgs args)
        {
            Console.WriteLine("Client connected: " + args.IpPort);
        }

        static void ClientDisconnected(ClientDisconnectedEventArgs args)
        {
            Console.WriteLine("Client disconnected: " + args.IpPort);
        }

        static async void MessageReceived(MessageReceivedEventArgs args)
        {
            string json = Encoding.UTF8.GetString(args.Data);
            Console.WriteLine($"Message received from {args.IpPort}:");
            Console.WriteLine(json);

            var rootSyntax = JsonParser.Parse(json);

            // Expect 0 errors.
            if (rootSyntax.Errors.Count == 0)
            {
                // Use green syntax nodes to avoid extra allocations.
                if (rootSyntax.Syntax.Green.ValueNode.ContentNode is GreenJsonMapSyntax map)
                {
                    Maybe<long> nonce = Maybe<long>.Nothing;

                    foreach (var (_, keySyntax, _, valueSyntax) in map.ValidKeyValuePairs())
                    {
                        if (keySyntax.Value == "nonce"
                            && valueSyntax is GreenJsonIntegerLiteralSyntax integer
                            && integer.Value < long.MaxValue)
                        {
                            nonce = (long)integer.Value;
                        }
                    }

                    if (nonce.IsJust(out long nonceValue))
                    {
                        await server.SendAsync(
                            args.IpPort,
                            $"{{\"nonce\":{nonceValue},\"type\":\"invalid\",\"message\":\"Not supported yet.\"}}");
                    }
                }
            }
            else
            {
                // Log errors to Console.
                rootSyntax.Errors.ForEach(error => Console.WriteLine($"{error.ErrorCode} at {error.Start}-{error.Start + error.Length}"));
            }
        }
    }
}
