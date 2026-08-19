using Bokmal.Api.Identity;
using Bokmal.Api.Services;
using Bokmal.Api.Startup;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBokmalDatabase(builder.Configuration);

// Injected rather than read from DateTimeOffset.UtcNow, which is banned in this solution.
// Loan due dates and overdue flags are time-dependent behaviour, and behaviour that reads
// the clock directly cannot be tested without waiting for the clock.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentBorrower, HeaderCurrentBorrower>();

builder.Services.AddScoped<ReadingTimeService>();
builder.Services.AddScoped<CatalogueService>();
builder.Services.AddScoped<DiscoveryService>();
builder.Services.AddScoped<LoanService>();

// The OpenAPI document generator reads the HTTP JSON options, the controllers read their
// own. Both have to agree or the document describes a different API than the one running.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    // ASP.NET Core's web defaults accept a number written as a string, so "42" and 42 both
    // bind to an int. That is reflected honestly in the OpenAPI document, which types every
    // integer as ["integer", "string"] -- and the generated TypeScript client then hands the
    // frontend `number | string` for every page count and copy count, which is unusable.
    //
    // The fix is to mean what the contract says rather than to tidy the document afterwards:
    // numbers are numbers. Nothing legitimate sends a quoted integer to this API.
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddProblemDetails();

// .NET's built-in OpenAPI document. The Next.js client is generated from it, so the
// document is not documentation -- it is the contract the frontend compiles against.
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    // The one exception with a meaningful status code. Everything else is a bug and should
    // look like one.
    if (exception is NotSignedInException notSignedIn)
    {
        await Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Not signed in",
            detail: notSignedIn.Message).ExecuteAsync(context);

        return;
    }

    await Results.Problem(statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
}));

app.MapOpenApi();
app.MapControllers();

// A liveness check that also proves the catalogue is reachable, which is the failure that
// actually happens: the app starts fine and the database is not there.
app.MapGet("/health", async ([FromServices] Bokmal.Database.BokmalDbContext context) =>
    Results.Ok(new { status = "ok", books = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
        .CountAsync(context.Books) }));

// Bringing the database up to date without serving anything. Useful in a deploy step, and
// it is how you produce a database for the entity generator to read without booting a web
// server first -- see the regeneration instructions in the README.
var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);

await app.InitialiseDatabaseAsync(seedDemoData: !migrateOnly);

if (migrateOnly)
    return;

app.Run();

/// <summary>Exposed so the integration tests can boot the real application.</summary>
public partial class Program;
