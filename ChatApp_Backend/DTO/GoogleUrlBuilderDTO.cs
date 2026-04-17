namespace ChatApp_Backend.DTO
{
    public class GoogleUrlBuilderDTO
    {
        public string ClientID { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public string ResponseType { get => "code"; }
        public string State { get => "RANDOM_CSRF_TOKEN"; }
        public string Scope { get => "openid%20email%20profile"; }
        public string AuthUri { get; set; }
        public string TokenUri { get; set; }

        public override string ToString()
        {
            return AuthUri +
                "?client_id=" + ClientID +
                "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
                "&response_type=" + ResponseType +
                "&scope=" + Scope +
                "&state=" + State;
        }
    }
}
