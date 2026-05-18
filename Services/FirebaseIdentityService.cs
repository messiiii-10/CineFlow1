using System.Net.Http.Json;
using System.Text.Json;
using CineFlow.Configuration;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Options;

namespace CineFlow.Services
{
    public class FirebaseIdentityService
    {
        private readonly FirebaseSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FirebaseIdentityService> _logger;

        public FirebaseIdentityService(
            IOptions<FirebaseSettings> options,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<FirebaseIdentityService> logger)
        {
            _settings = options.Value;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;

            FirebaseAdminInitializer.TryInitialize(_settings, environment.ContentRootPath, logger);
        }

        public bool IsEnabled => _settings.UseFirebaseAuth;

        public bool IsConfigured => IsEnabled && FirebaseAdminInitializer.IsReady;

        public string ConfigurationErrorMessage =>
            FirebaseAdminInitializer.InitializationError
            ?? "Firebase ayarlari eksik. API key, project id ve service account bilgilerini tamamla.";

        public bool IsAdminEmail(string email)
        {
            var adminEmail = _configuration["AdminContact:Email"] ?? "admin@gmail.com";
            return adminEmail.Equals(email?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public Task<FirebaseAuthResult> SignInWithEmailPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
            => ExecuteAuthRequestAsync(
                "accounts:signInWithPassword",
                new FirebasePasswordRequest(email, password),
                cancellationToken);

        public Task<FirebaseAuthResult> RegisterWithEmailPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
            => ExecuteAuthRequestAsync(
                "accounts:signUp",
                new FirebasePasswordRequest(email, password),
                cancellationToken);

        private async Task<FirebaseAuthResult> ExecuteAuthRequestAsync(string endpoint, FirebasePasswordRequest payload, CancellationToken cancellationToken)
        {
            if (!IsEnabled)
                return FirebaseAuthResult.Failure("Firebase kimlik dogrulama kapali.");

            if (!IsConfigured)
                return FirebaseAuthResult.Failure(ConfigurationErrorMessage);

            try
            {
                var httpClient = _httpClientFactory.CreateClient(nameof(FirebaseIdentityService));
                var response = await httpClient.PostAsJsonAsync(
                    $"https://identitytoolkit.googleapis.com/v1/{endpoint}?key={_settings.ApiKey}",
                    payload,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorPayload = await response.Content.ReadFromJsonAsync<FirebaseErrorEnvelope>(cancellationToken: cancellationToken);
                    return FirebaseAuthResult.Failure(MapFirebaseError(errorPayload?.Error?.Message));
                }

                var authResponse = await response.Content.ReadFromJsonAsync<FirebaseAuthApiResponse>(cancellationToken: cancellationToken);
                if (authResponse is null || string.IsNullOrWhiteSpace(authResponse.IdToken))
                    return FirebaseAuthResult.Failure("Firebase yanitinda id token bulunamadi.");

                var verifiedUser = await VerifyUserAsync(authResponse, cancellationToken);
                return FirebaseAuthResult.Success(verifiedUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firebase auth istegi basarisiz oldu.");
                return FirebaseAuthResult.Failure("Firebase baglantisi sirasinda bir hata olustu.");
            }
        }

        private static async Task<FirebaseAuthUser> VerifyUserAsync(FirebaseAuthApiResponse authResponse, CancellationToken cancellationToken)
        {
            var firebaseAuth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
            if (firebaseAuth is null)
                throw new InvalidOperationException("Firebase Admin SDK hazir degil.");

            var token = await firebaseAuth.VerifyIdTokenAsync(authResponse.IdToken, cancellationToken);
            var userRecord = await firebaseAuth.GetUserAsync(token.Uid, cancellationToken);

            return new FirebaseAuthUser(
                token.Uid,
                userRecord.Email ?? authResponse.Email ?? string.Empty,
                userRecord.DisplayName);
        }

        private static string MapFirebaseError(string? errorCode)
        {
            return errorCode switch
            {
                "EMAIL_EXISTS" => "Bu e-posta ile daha once kayit yapilmis.",
                "OPERATION_NOT_ALLOWED" => "Firebase tarafinda Email/Password girisi henuz acik degil.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Cok fazla deneme yapildi. Biraz sonra tekrar dene.",
                "EMAIL_NOT_FOUND" => "Bu e-posta ile eslesen bir hesap bulunamadi.",
                "INVALID_PASSWORD" => "Sifre hatali.",
                "INVALID_LOGIN_CREDENTIALS" => "E-posta veya sifre hatali.",
                "USER_DISABLED" => "Bu hesap devre disi birakilmis.",
                "INVALID_EMAIL" => "Gecerli bir e-posta giriniz.",
                "MISSING_PASSWORD" => "Sifre zorunludur.",
                "MISSING_EMAIL" => "E-posta zorunludur.",
                var value when value != null && value.StartsWith("WEAK_PASSWORD", StringComparison.OrdinalIgnoreCase)
                    => "Sifre Firebase kurallarina gore zayif. En az 6 karakter kullan.",
                _ => "Firebase kimlik dogrulama islemi tamamlanamadi."
            };
        }

        private sealed record FirebasePasswordRequest(string Email, string Password)
        {
            public bool ReturnSecureToken { get; init; } = true;
        }

        private sealed class FirebaseAuthApiResponse
        {
            public string IdToken { get; set; } = string.Empty;

            public string LocalId { get; set; } = string.Empty;

            public string Email { get; set; } = string.Empty;
        }

        private sealed class FirebaseErrorEnvelope
        {
            public FirebaseErrorBody? Error { get; set; }
        }

        private sealed class FirebaseErrorBody
        {
            public string? Message { get; set; }
        }
    }

    public sealed record FirebaseAuthUser(string Uid, string Email, string? DisplayName);

    public sealed record FirebaseAuthResult(bool Succeeded, FirebaseAuthUser? User, string? ErrorMessage)
    {
        public static FirebaseAuthResult Success(FirebaseAuthUser user) => new(true, user, null);

        public static FirebaseAuthResult Failure(string message) => new(false, null, message);
    }
}
