using Resource_Management_System.Data;
using Resource_Management_System.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Globalization;

// The UI text is all hardcoded Arabic, but numbers (quantities, dates, IDs, etc.)
// must always render with English/Latin digits (0-9), never Eastern Arabic-Indic
// digits (٠١٢...). Forcing a fixed English-digit culture as the app-wide default
// (rather than relying on whatever locale the host OS happens to be set to)
// guarantees this regardless of where the app is deployed.
var numberCulture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
CultureInfo.DefaultThreadCurrentCulture = numberCulture;
CultureInfo.DefaultThreadCurrentUICulture = numberCulture;

var builder = WebApplication.CreateBuilder(args);

// Register services
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    // Replace the default English model-binding error messages with Arabic ones
    // (these fire before our own [Range]/[Required] validation runs, e.g. when a
    // required field is left empty or a whole-number field receives a fraction).
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(
        value => $"القيمة المدخلة غير صالحة.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor(
        (value, field) => $"القيمة المدخلة غير صالحة.");
    options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(
        () => "هذا الحقل مطلوب.");
    options.ModelBindingMessageProvider.SetMissingRequestBodyRequiredValueAccessor(
        () => "هذا الحقل مطلوب.");
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(
        value => "هذا الحقل مطلوب.");
    options.ModelBindingMessageProvider.SetNonPropertyAttemptedValueIsInvalidAccessor(
        value => "القيمة المدخلة غير صالحة.");
    options.ModelBindingMessageProvider.SetUnknownValueIsInvalidAccessor(
        field => "القيمة المدخلة غير صالحة.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(
        field => "يجب أن تكون القيمة رقمًا صحيحًا.");
    options.ModelBindingMessageProvider.SetNonPropertyValueMustBeANumberAccessor(
        () => "يجب أن تكون القيمة رقمًا صحيحًا.");
});
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddScoped<DbHelper>();
builder.Services.AddScoped<CookieSessionValidationEvents>();
builder.Services.AddSingleton<QueueUpdateNotifier>();

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-ResourceManagement.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});

// Cookie-based login — this is what [Authorize] and HttpContext.SignInAsync rely on
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // logs out after 8 hours idle
        options.SlidingExpiration = true;
        options.Cookie.Name = "__Host-ResourceManagement.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.EventsType = typeof(CookieSessionValidationEvents);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Belt-and-braces: also pin the culture per-request so English digits are
// used no matter what culture a client's Accept-Language header requests.
var englishDigitsCulture = new[] { numberCulture };
app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(numberCulture, numberCulture),
    SupportedCultures = englishDigitsCulture,
    SupportedUICultures = englishDigitsCulture
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();   // must come before UseAuthorization

// A department account created by the administrator starts with a temporary
// password. It cannot open the application until its owner has replaced it.
app.Use(async (context, next) =>
{
    var mustChangePassword = context.User.Identity?.IsAuthenticated == true
        && string.Equals(context.User.FindFirst("MustChangePassword")?.Value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
    var path = context.Request.Path;
    if (mustChangePassword
        && !path.StartsWithSegments("/Account/ChangePassword")
        && !path.StartsWithSegments("/Account/Logout"))
    {
        context.Response.Redirect("/Account/ChangePassword");
        return;
    }
    await next();
});
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
