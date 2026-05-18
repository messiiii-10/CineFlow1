namespace CineFlow.Configuration
{
    public class FirebaseSettings
    {
        public string ApiKey { get; set; } = string.Empty;

        public string ProjectId { get; set; } = string.Empty;

        public string CredentialsPath { get; set; } = string.Empty;

        public bool UseFirebaseAuth => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
