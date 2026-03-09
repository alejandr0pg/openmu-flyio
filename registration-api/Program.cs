using RegistrationApi.Models;
using RegistrationApi.Services;

var builder = WebApplication.CreateBuilder(args);

var dbConnection = builder.Configuration["DB_CONNECTION"];
if (string.IsNullOrEmpty(dbConnection))
{
    var host = builder.Configuration["DB_HOST"];
    var port = builder.Configuration["DB_PORT"] ?? "5432";
    var user = builder.Configuration["DB_ADMIN_USER"];
    var pass = builder.Configuration["DB_ADMIN_PW"];
    var ssl = builder.Configuration["DB_SSL"] ?? "Require";
    var timeout = builder.Configuration["DB_TIMEOUT"] ?? "30";

    if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user))
        dbConnection = $"Host={host};Port={port};Username={user};Password={pass};Database=openmu;SSL Mode={ssl};Timeout={timeout};Trust Server Certificate=true";
    else
        throw new InvalidOperationException("DB_CONNECTION or DB_HOST/DB_ADMIN_USER not configured");
}

var repo = new AccountRepository(dbConnection);
var rateLimiter = new RateLimiter(maxRequests: 3, window: TimeSpan.FromHours(1));
var oauthSessions = new OAuthSessionStore();

var googleClientId = builder.Configuration["OAUTH_GOOGLE_CLIENT_ID"] ?? "";
var googleClientSecret = builder.Configuration["OAUTH_GOOGLE_CLIENT_SECRET"] ?? "";
var oauthRedirectUri = builder.Configuration["OAUTH_REDIRECT_URI"] ?? "https://lara-mu.fly.dev/auth/callback";
var oauthSecret = builder.Configuration["REGISTRATION_SECRET"] ?? "default-oauth-secret";

var googleToken = new GoogleTokenService(googleClientId, googleClientSecret, oauthRedirectUri);
var oauthAccount = new OAuthAccountService(repo, oauthSecret);

var cleanupTimer = new Timer(_ =>
{
    rateLimiter.Cleanup();
    oauthSessions.Cleanup();
}, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

// Apple redirects here via form_post with id_token
app.MapPost("/auth/callback", async (HttpContext ctx) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    var form = await ctx.Request.ReadFormAsync();
    var idToken = form["id_token"].ToString();
    var state = form["state"].ToString();

    if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(state))
        return Results.Content(HtmlPage("Login Failed", "Missing token or state."), "text/html");

    try
    {
        var email = AppleTokenService.ExtractEmailFromJwt(idToken);
        if (string.IsNullOrEmpty(email))
        {
            oauthSessions.Fail(state, "Could not retrieve email from Apple.");
            return Results.Content(HtmlPage("Login Failed", "Could not retrieve your email. Please try again."), "text/html");
        }

        var (username, password) = await oauthAccount.GetOrCreateAccountAsync(email);
        oauthSessions.Complete(state, username, password);
        logger.LogInformation("Apple OAuth login for {Email} -> {Username}", email, username);

        var deepLink = $"laramu://oauth-done?state={state}";
        return Results.Content(HtmlPage("Login Successful", "Returning to the game...", deepLink), "text/html");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Apple OAuth callback failed for state {State}", state);
        oauthSessions.Fail(state, "Server error during authentication.");
        return Results.Content(HtmlPage("Login Failed", "An error occurred. Please try again."), "text/html");
    }
});

// Google redirects here after user authenticates
app.MapGet("/auth/callback", async (string? code, string? state, HttpContext ctx) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();

    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        return Results.BadRequest("Missing code or state");

    try
    {
        var email = await googleToken.ExchangeCodeForEmailAsync(code);
        if (string.IsNullOrEmpty(email))
        {
            oauthSessions.Fail(state, "Could not retrieve email from Google.");
            return Results.Content(HtmlPage("Login Failed", "Could not retrieve your email. Please try again."), "text/html");
        }

        var (username, password) = await oauthAccount.GetOrCreateAccountAsync(email);
        oauthSessions.Complete(state, username, password);
        logger.LogInformation("OAuth login for {Email} -> {Username}", email, username);

        // Deep link redirect — brings mobile app back to foreground
        var deepLink = $"laramu://oauth-done?state={state}";
        return Results.Content(HtmlPage("Login Successful",
            "Returning to the game...",
            deepLink), "text/html");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "OAuth callback failed for state {State}", state);
        oauthSessions.Fail(state, "Server error during authentication.");
        return Results.Content(HtmlPage("Login Failed", "An error occurred. Please try again."), "text/html");
    }
});

// Client polls this endpoint to get the OAuth result
app.MapGet("/auth/poll", (string? id) =>
{
    if (string.IsNullOrEmpty(id))
        return Results.BadRequest("Missing id");

    var session = oauthSessions.Get(id, consume: true);
    if (session == null)
        return Results.Json(new { status = "pending" });

    return session.Status switch
    {
        "ok" => Results.Json(new { status = "ok", username = session.Username, password = session.Password }),
        "error" => Results.Json(new { status = "error", message = session.Error }),
        _ => Results.Json(new { status = "pending" })
    };
});

app.MapPost("/auth/apple", async (HttpContext ctx) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();

    using var reader = new StreamReader(ctx.Request.Body);
    var jwt = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(jwt))
        return Results.BadRequest("Missing token");

    var email = AppleTokenService.ExtractEmailFromJwt(jwt.Trim());
    if (string.IsNullOrEmpty(email))
    {
        logger.LogWarning("Apple JWT missing email claim");
        return Results.Json(new { status = "error", message = "Could not extract email from token." }, statusCode: 400);
    }

    try
    {
        var (username, password) = await oauthAccount.GetOrCreateAccountAsync(email);
        logger.LogInformation("Apple Sign In for {Email} -> {Username}", email, username);
        return Results.Json(new { status = "ok", username, password });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Apple auth failed for {Email}", email);
        return Results.Json(new { status = "error", message = "Server error during authentication." }, statusCode: 500);
    }
});

app.MapPost("/api/register", async (RegisterRequest req, HttpContext ctx) =>
{
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();

    if (!rateLimiter.IsAllowed(ip))
    {
        logger.LogWarning("Rate limit exceeded for IP {Ip}", ip);
        return Results.Json(new RegisterResponse(false, "Too many requests. Try again later."),
            statusCode: 429);
    }

    if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 4 || req.Username.Length > 10)
        return Results.Json(new RegisterResponse(false, "Username must be 4-10 characters."),
            statusCode: 400);

    if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 4 || req.Password.Length > 20)
        return Results.Json(new RegisterResponse(false, "Password must be 4-20 characters."),
            statusCode: 400);

    if (!req.Username.All(char.IsLetterOrDigit))
        return Results.Json(new RegisterResponse(false, "Username must be alphanumeric."),
            statusCode: 400);

    try
    {
        if (await repo.ExistsAsync(req.Username))
            return Results.Json(new RegisterResponse(false, "Username already exists."),
                statusCode: 409);

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 11);
        await repo.CreateAccountAsync(req.Username, hash);

        logger.LogInformation("Account '{User}' registered from IP {Ip}", req.Username, ip);
        return Results.Json(new RegisterResponse(true, "Account created successfully."),
            statusCode: 201);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Registration failed for '{User}'", req.Username);
        return Results.Json(new RegisterResponse(false, "Server error. Try again later."),
            statusCode: 500);
    }
});

app.Run();

static string HtmlPage(string title, string message, string? redirectUrl = null)
{
    var redirect = redirectUrl != null
        ? "<script>setTimeout(function(){window.location.href='" + redirectUrl + "';},500);</script>"
        : "";
    return "<!DOCTYPE html><html><head><title>" + title + "</title>" + redirect +
        "<style>body{font-family:sans-serif;display:flex;justify-content:center;align-items:center;" +
        "height:100vh;margin:0;background:#1a1a2e;color:#e0e0e0}" +
        ".card{text-align:center;padding:2rem;border-radius:12px;background:#16213e;" +
        "box-shadow:0 4px 20px rgba(0,0,0,.3)}</style></head>" +
        "<body><div class='card'><h1>" + title + "</h1><p>" + message + "</p></div></body></html>";
}
