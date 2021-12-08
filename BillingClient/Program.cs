using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BillingClient
{
    class Program
    {
        static readonly List<Item> items = new List<Item>()
        {
            new Item() { ItemName = "Pineapple",  Price = 100 },
            new Item() { ItemName = "Oranges", Price = 20 },
            new Item() { ItemName = "Banana",  Price = 10 },
        };

        /// <summary>
        /// Sample client code that makes calls to the Billing gRPC server
        /// </summary>
        class BillingClient
        {
            readonly Billing.BillingClient client;

            public BillingClient(Billing.BillingClient client)
            {
                this.client = client;
            }

            /// <summary>
            /// Asynchronous unary RPC example
            /// Client sends a hello request to the server and server returns a hello reply.
            /// </summary>
            public async Task Hello()
            {
                try
                {
                    // Client sending a single message to server and receiving a single response. (Say hi to the storekeeper)
                    using var call = client.SayHelloAsync(new HelloRequest { Name = "Anne", Message = "Hi!" });

                    var reply = await call.ResponseAsync;

                    Console.WriteLine(reply.Message);
                }
                catch (RpcException rpe)
                {
                    string errorDetails = string.Concat(typeof(RpcException), "_", rpe.StackTrace, rpe.StatusCode, rpe.Message);
                    Console.WriteLine("Exception..." + errorDetails);
                }
            }

            /// <summary>
            /// Server streaming RPC example.
            /// Call the server to get a stream of all the items in stock (ask the store keeper which fruits they have in stock)
            /// </summary>
            /// <returns></returns>
            public async Task GetAllItemsAsStream()
            {
                try
                {
                    using (var call = client.GetItemsInStock(new AllItemsRequest{ Message = "What do you have in stock ?" }))
                    {
                        var responseStream = call.ResponseStream;

                        while (await responseStream.MoveNext())
                        {
                            Item item = responseStream.Current;
                            Console.WriteLine(item.ItemName + " for " + item.Price + " a piece.");
                        }
                    }
                }
                catch (RpcException rpe)
                {
                    string errorDetails = string.Concat(typeof(RpcException), "_", rpe.StackTrace, rpe.StatusCode, rpe.Message);
                    Console.WriteLine("Exception..." + errorDetails);
                }
            }

            /// <summary>
            /// Client streaming RPC example
            /// Send the cart items to the server as a stream and receive one single response (the final bill) from the server.
            /// </summary>
            /// <returns></returns>
            public async Task SendCartItemsAsStream()
            {
                try
                {
                    using (var call = client.ProcessAllItems())
                    {
                        Random rand = new Random();
                        for (int i = 0; i < 3; i++)
                        {
                            // Build the cart item object
                            Item item = items[i];

                            CartItem cartItem = new CartItem()
                            {
                                Item = item,
                                Quantity = i * 10 + 10
                            };

                            Console.WriteLine(cartItem.Quantity + " pieces of " + item.ItemName);

                            await call.RequestStream.WriteAsync(cartItem);

                            // adding a bit of delay before sending the next item request
                            await Task.Delay(rand.Next(1000) + 500);
                        }

                        await call.RequestStream.CompleteAsync();

                        Bill finalBill = await call.ResponseAsync;

                        Console.WriteLine("Received " + finalBill.BillType + " for a total of " + finalBill.TotalQuantity + " items and a total amount of " + finalBill.TotalPrice);
                    }
                }
                catch (RpcException rpe)
                {
                    string errorDetails = string.Concat(typeof(RpcException), "_", rpe.StackTrace, rpe.StatusCode, rpe.Message);
                    Console.WriteLine("Exception..." + errorDetails);
                }
            }

            /// <summary>
            /// Bi-directional streaming RPC example
            /// Send a stream of items to the server and get an interim bill based on the items processed so far.
            /// </summary>
            /// <returns></returns>
            public async Task ProcessItemsOneByOne()
            {
                try
                {
                    using (var call = client.ProcessEachItemOneByOne())
                    {
                        Random rand = new Random();
                        for (int i = 0; i < 3; i++)
                        {
                            Item item = items[i];

                            CartItem cartItem = new CartItem()
                            {
                                Item = item,
                                Quantity = i * 10 + 10
                            };

                            Console.WriteLine(cartItem.Quantity + " pieces of " + item.ItemName);

                            await call.RequestStream.WriteAsync(cartItem);

                            // Check if there is a response before we send the next request
                            if (await call.ResponseStream.MoveNext())
                            {
                                var tempBill = call.ResponseStream.Current;
                                Console.WriteLine("Received " + tempBill.BillType + " for a total of " + tempBill.TotalQuantity + " items and a total amount of " + tempBill.TotalPrice);
                            }

                            // adding a bit of delay before sending the next item request
                            await Task.Delay(rand.Next(1000) + 500);
                        }

                        // We're done sending items to the server
                        await call.RequestStream.CompleteAsync();

                        // Check if we have a final message in the response stream
                        await call.ResponseStream.MoveNext();
                        Bill finalBill = call.ResponseStream.Current;

                        Console.WriteLine("Received " + finalBill.BillType + " for a total of " + finalBill.TotalQuantity + " items and a total amount of " + finalBill.TotalPrice);
                    }
                }
                catch (RpcException rpe)
                {
                    string errorDetails = string.Concat(typeof(RpcException), "_", rpe.StackTrace, rpe.StatusCode, rpe.Message);
                    Console.WriteLine("Exception..." + errorDetails);
                }
            }

        }
        static async Task Main(string[] args)
        {
            // Comment this out to enable logging
            var loggerFactory = LoggerFactory.Create(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            });

            var channel = GrpcChannel.ForAddress("https://localhost:5001",
                 new GrpcChannelOptions { LoggerFactory = loggerFactory });


            // The port number(5001) must match the port of the gRPC server.
            // var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = new BillingClient(new Billing.BillingClient(channel));

            // Send an initial request from client to server
            Console.WriteLine("==========================================");
            Console.WriteLine("Unary RPC Call");
            await client.Hello();
            Console.WriteLine("==========================================");

            // Request for all items in stock
            Console.WriteLine("==========================================");
            Console.WriteLine("Server Streaming RPC Call");
            Console.WriteLine("What do you have in stock ?");

            await client.GetAllItemsAsStream();
            Console.WriteLine("==========================================");

            // Make an order and get a final bill for everything
            Console.WriteLine("==========================================");
            Console.WriteLine("Client Streaming RPC Call");
            Console.WriteLine("I'll take two orders. ");
            Console.WriteLine("For the first one, I just need the final bill ");
            await client.SendCartItemsAsStream();
            Console.WriteLine("==========================================");

            // Make an order and get an interim bill after each item
            Console.WriteLine("Bidirectional RPC Calls");
            Console.WriteLine("==========================================");
            Console.WriteLine("For the second one, I'll need a running bill after each item");
            await client.ProcessItemsOneByOne();
            Console.WriteLine("==========================================");
        }
    }
}
