using Microsoft.Extensions.Configuration;

namespace BlobsProducer.Configurations
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
