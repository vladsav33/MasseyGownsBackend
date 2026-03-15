namespace GownApi.Model
{
    public class PaystationOptions
    {
        public string ClientSecret { get; set; }
        public string PaystationId { get; set; }
        public string GatewayId { get; set; }
        public string BaseUrl { get; set; }
        public string ReturnUrl { get; set; }
        public string RefundUrl { get; set; }
        public string PayMePurchaseUrl { get; set; }
        public string OAuthUrl { get; set; }
        public string ResponseUrl { get; set; }
        public bool TestMode { get; set; }
        public string? HmacKey { get; set; }

    }
}
