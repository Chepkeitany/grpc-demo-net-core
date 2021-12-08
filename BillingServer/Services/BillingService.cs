using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BillingServer
{
    public class BillingService : Billing.BillingBase
    {
        private readonly ILogger<BillingService> _logger;
        private readonly List<Item> items;
        public BillingService(ILogger<BillingService> logger)
        {
            _logger = logger;
            this.items = getAllItems();
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            // Console.WriteLine(context.Host);
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name + ". How can I help you?",
                Name = "John"
            });
        }

        public override async Task GetItemsInStock(AllItemsRequest request,
                IServerStreamWriter<Item> responseStream,
                ServerCallContext context)
        {
            Console.WriteLine(request.Message);
            foreach (var item in this.items)
            {
                await responseStream.WriteAsync(item);
            }
        }

        /// <summary>
        /// Get a stream of cart items from the client and calculate a final bill.
        /// </summary>
        /// <param name="requestStream"></param>
        /// <returns></returns>
        public override async Task<Bill> ProcessAllItems(IAsyncStreamReader<CartItem> requestStream, ServerCallContext context)
        {
            float totalPrice = 0;
            int totalQuantity = 0;

            List<Item> items = this.items;

            while (await requestStream.MoveNext())
            {
                var cartItem = requestStream.Current;

                Item itemInStore = items.FirstOrDefault((item) => item.ItemName.Equals(cartItem.Item.ItemName));

                Console.WriteLine("Received an order for " + cartItem.Quantity + " pieces of " + cartItem.Item.ItemName);

                totalPrice += (itemInStore.Price * cartItem.Quantity);
                totalQuantity += cartItem.Quantity;
            }

            return new Bill
            {
                BillType = "Final Bill",
                TotalQuantity = totalQuantity,
                TotalPrice = totalPrice
            };

        }

        /// <summary>
        /// Get a stream of cart items from the client and calculate a final bill.
        /// </summary>
        /// <param name="requestStream"></param>
        /// <param name="responseStream"></param>
        /// <returns></returns>
        public override async Task ProcessEachItemOneByOne(IAsyncStreamReader<CartItem> requestStream, IServerStreamWriter<Bill> responseStream, ServerCallContext context)
        {
            float totalPrice = 0;
            int totalQuantity = 0;

            List<Item> items = this.items;

            while (await requestStream.MoveNext())
            {
                var cartItem = requestStream.Current;

                // Filter out the specific item so that we can get the price of the item
                Item itemInStore= items.FirstOrDefault((item) => item.ItemName.Equals(cartItem.Item.ItemName));

                Console.WriteLine("An order for " + cartItem.Quantity + " pieces of " + cartItem.Item.ItemName);

                totalPrice += (itemInStore.Price * cartItem.Quantity);
                totalQuantity += cartItem.Quantity;

                Bill tempBill = new Bill
                {
                    BillType = "Interim Bill",
                    TotalQuantity = totalQuantity,
                    TotalPrice = totalPrice
                };

                await responseStream.WriteAsync(tempBill);
            }

            Bill finalBill = new Bill
            {
                BillType = "Final Bill",
                TotalQuantity = totalQuantity,
                TotalPrice = totalPrice
            };

            await responseStream.WriteAsync(finalBill);
        }

        private List<Item> getAllItems()
        {
            List<Item> allItems = new List<Item>()
            {
                new Item() { ItemName = "Pineapple",  Price = 100 },
                new Item() { ItemName = "Oranges", Price = 20 },
                new Item() { ItemName = "Banana",  Price = 10 },
            };

            return allItems;
        }
    }
}
