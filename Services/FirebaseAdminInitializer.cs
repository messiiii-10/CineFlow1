using CineFlow.Configuration;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace CineFlow.Services
{
    public static class FirebaseAdminInitializer
    {
        private static readonly object SyncRoot = new();

        public static string? InitializationError { get; private set; }

        public static bool IsReady => FirebaseApp.DefaultInstance != null;

        public static void TryInitialize(FirebaseSettings settings, string contentRootPath, ILogger logger)
        {
            if (!settings.UseFirebaseAuth || IsReady)
                return;

            lock (SyncRoot)
            {
                if (IsReady)
                    return;

                try
                {
                    var credential = BuildCredential(settings, contentRootPath);
                    if (credential is null)
                    {
                        InitializationError = "Firebase service account dosyasi bulunamadi. appsettings.json icindeki Firebase:CredentialsPath alanini doldur.";
                        logger.LogWarning("Firebase Admin SDK baslatilamadi: credentials path bulunamadi.");
                        return;
                    }

                    var options = new AppOptions
                    {
                        Credential = credential
                    };

                    if (!string.IsNullOrWhiteSpace(settings.ProjectId))
                        options.ProjectId = settings.ProjectId;

                    FirebaseApp.Create(options);
                    InitializationError = null;
                }
                catch (Exception ex)
                {
                    InitializationError = $"Firebase Admin SDK baslatilamadi: {ex.Message}";
                    logger.LogWarning(ex, "Firebase Admin SDK baslatilirken hata olustu.");
                }
            }
        }

        private static GoogleCredential? BuildCredential(FirebaseSettings settings, string contentRootPath)
        {
            var credentialsPath = settings.CredentialsPath?.Trim();
            if (!string.IsNullOrWhiteSpace(credentialsPath))
            {
                var resolvedPath = Path.IsPathRooted(credentialsPath)
                    ? credentialsPath
                    : Path.Combine(contentRootPath, credentialsPath);

                if (File.Exists(resolvedPath))
                    return CredentialFactory
                        .FromFile<ServiceAccountCredential>(resolvedPath)
                        .ToGoogleCredential();
            }

            var googleApplicationCredentials = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (!string.IsNullOrWhiteSpace(googleApplicationCredentials))
                return GoogleCredential.GetApplicationDefault();

            return null;
        }
    }
}
