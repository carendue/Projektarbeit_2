using Microsoft.Extensions.Configuration;

namespace BlobsConsumer.Configurations
{
    public class MyName
    {
        public string Value { get; }

        public MyName(IConfiguration configuration)
        {
            Value = configuration["MY_NAME"] ?? "A";
        }
    }
}
