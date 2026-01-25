using Serilog.Core;
using Serilog.Events;

namespace GownApi.Logging
{
    public sealed class OrderTagEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
       
            if (logEvent.Properties.ContainsKey("OrderTag"))
                return;

            if (logEvent.Properties.TryGetValue("OrderNo", out var val))
            {
                var orderNo = (val as ScalarValue)?.Value?.ToString();

                if (!string.IsNullOrWhiteSpace(orderNo))
                {
         
                    var tag = $" (Order:{orderNo})";
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OrderTag", tag));
                }
            }
         
        }
    }
}