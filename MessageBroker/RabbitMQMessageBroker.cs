using System.Net;
using System.Text;
using System.Threading.Channels;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageBroker;

public enum BrokerQueue
{
    ModelCalculation,
    ModelReadyCalculation,
    ModelResult,
    Hakasia,
}

public class RabbitMQMessageBroker(
    string hostname,
    string user,
    string password,
    BrokerQueue queue,
    string? resultQueueName = null) : IMessageBroker
{
    public string QueueName => queue switch
    {
        BrokerQueue.ModelCalculation => "model-calculation",
        BrokerQueue.ModelReadyCalculation => "model-ready-calculation",
        BrokerQueue.ModelResult =>
            $"model-result{resultQueueName ?? throw new ArgumentException("if BrokerQueue is ModelResult resultQueueName can't be null")}",
        _ => throw new ArgumentOutOfRangeException(nameof(queue), queue, null)
    };

    private CancellationToken ConsumeCancellationToken { get; set; }
    private CancellationToken PublishCancellationToken { get; set; }

    private Queue<byte[]> MessageStack { get; set; } = new();

    private Task? PublisherTask { get; set; }

    private ConnectionFactory MakeFactory() => new()
    {
        HostName = hostname,
        CredentialsProvider = !string.IsNullOrEmpty(user) & !string.IsNullOrEmpty(password)
            ? new BasicCredentialsProvider(user, password)
            : null,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        RequestedHeartbeat = TimeSpan.FromSeconds(60),
        AutomaticRecoveryEnabled = true
    };


    public CancellationTokenSource? PublishCancelTokenSource { get; set; }


    public CancellationTokenSource ConsumeCancelTokenSource { get; set; } = new();


    public Task PublishMessage(string message)
    {
        var messageBytes = Encoding.ASCII.GetBytes(message);
        return Task.FromResult(PublishMessage(messageBytes));
    }

    public void PurgeQueue()
    {
        using var connection = MakeFactory().CreateConnection();
        using var channel = connection.CreateModel();
        channel.QueuePurge(QueueName);
    }

    public int GetConsumersCount()
    {
        using var connection = MakeFactory().CreateConnection();
        using var channel = connection.CreateModel();
        return (int)channel.ConsumerCount(QueueName);
    }

    private async Task PublisherAsync(int reloadPublisherInSecond = 5)
    {
        try
        {
            PublishCancelTokenSource = new();
            PublishCancellationToken = PublishCancelTokenSource.Token;

            _ = Task.Run(() =>
            {
                Thread.Sleep(reloadPublisherInSecond * 1000);
                PublishCancelTokenSource.Cancel();
            });

            using var connection = MakeFactory().CreateConnection();
            using var channel = connection.CreateModel();

            //var properties = channel.CreateBasicProperties();

            //// one day
            //var ttlMilliseconds = 24 * 60 * 60 * 1000;
            //properties.Headers = new Dictionary<string, object>
            //{
            //    // Время жизни сообщения. После исчерпания временного лимита сообщени удаляется из очереди.
            //    { "x-message-ttl", ttlMilliseconds }
            //};

            var rabbitQueue = channel.QueueDeclare(queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: queue == BrokerQueue.ModelResult,
                arguments: null);


            while (!PublishCancellationToken.IsCancellationRequested)
            {
                if (MessageStack.TryDequeue(out byte[]? stackMessage))
                {
                    channel.BasicPublish(exchange: "",
                        routingKey: QueueName,
                        basicProperties: null,
                        //basicProperties: properties,
                        body: stackMessage);
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task PublishMessage(byte[] message)
    {
        MessageStack.Enqueue(message);
        PublisherTask = PublisherTask?.IsCompleted ?? true
            ? PublisherAsync()
            : PublisherTask;
    }

    public async Task ConsumeMessageAsync(Action<byte[]> job)
    {
        ConsumeCancelTokenSource = new();
        ConsumeCancellationToken = ConsumeCancelTokenSource.Token;

        using var connection = MakeFactory().CreateConnection();

        using var channel = connection.CreateModel();

        channel.BasicQos(0, 1, true);
        channel.QueueDeclare(queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: queue == BrokerQueue.ModelResult,
            arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (_, e) =>
        {
            job(e.Body.ToArray());
            channel.BasicAck(e.DeliveryTag, false);
        };

        channel.BasicConsume(queue: QueueName,
            autoAck: false,
            consumer: consumer,
            consumerTag: Dns.GetHostName());

        while (!ConsumeCancellationToken.IsCancellationRequested) await Task.Delay(1000);
    }

}