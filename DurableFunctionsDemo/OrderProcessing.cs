using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableFunctionsDemo
{
    public static class OrderProcessing
    {
        [Function(nameof(OrderProcessing))]
        public static async Task<Order> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(OrderProcessing));
            
            logger.LogInformation("Starting OrderProcessing");

            var order = context.GetInput<Order>();

            var orderAfterInventoryCheck = await context.CallActivityAsync<Order>(nameof(CheckInventory), order);

            var orderAfterCustomerCheck = await context.CallActivityAsync<Order>(nameof(CheckCustomer), orderAfterInventoryCheck);

            var orderAfterFullfillmentCheck = await context.CallActivityAsync<Order>(nameof(CheckFullfillment), orderAfterCustomerCheck);

            if (orderAfterFullfillmentCheck.CanFullfill)
            {
                var orderAfterFullfillment = await context.CallActivityAsync<Order>(nameof(CheckFullfillment), orderAfterInventoryCheck);
                return orderAfterFullfillment;
            }

            return orderAfterFullfillmentCheck;
        }

        [Function(nameof(CheckInventory))]
        public static Order CheckInventory([ActivityTrigger] Order order, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("CheckInventory");
            
            logger.LogInformation("Checking inventory for order {OrderId}", order.Id);

            order.InventoryChecked = CreateRandomBoolValue();
            order.Status = "INVENTORY_DONE";
            
            return order;
        }

        [Function(nameof(CheckCustomer))]
        public static Order CheckCustomer([ActivityTrigger] Order order, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("CheckCustomer");

            logger.LogInformation("Checking customer for order {OrderId}", order.Id);

            order.CustomerChecked = CreateRandomBoolValue();
            order.Status = "CUSTOMER_DONE";

            return order;
        }

        [Function(nameof(CheckFullfillment))]
        public static Order CheckFullfillment([ActivityTrigger] Order order, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("CheckFullfillment");

            logger.LogInformation("Checking fullfillment for order {OrderId}", order.Id);

            order.CanFullfill = CreateRandomBoolValue();
            order.Status = "FULLFILLMENT_DONE";

            return order;
        }

        [Function(nameof(SendToFullfillment))]
        public static Order SendToFullfillment([ActivityTrigger] Order order, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SendToFullfillment");

            logger.LogInformation("Sending order to fullfillment. {OrderId}", order.Id);

            order.Status = "HANDLED";

            return order;
        }

        [Function("OrderProcessingTrigger")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("OrderProcessingTrigger");

            var receivedOrder = new Order
            {
                Id = Guid.NewGuid()
            };

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(OrderProcessing), receivedOrder);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }

        private static bool CreateRandomBoolValue()
        {
            var random = new Random();

            return random.Next(2) == 1;
        }
    }
}
