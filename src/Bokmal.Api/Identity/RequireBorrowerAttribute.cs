using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bokmal.Api.Identity;

/// <summary>
/// Refuses the request with 401 unless it identifies a known borrower.
///
/// Deliberately a filter rather than a check inside each action. Not identifying yourself
/// is an ordinary outcome, not a failure, and letting it surface as an exception meant every
/// anonymous request was logged as an unhandled error -- which trains people to ignore the
/// error log, the one thing it must never do.
///
/// It also puts the guard where <c>[Authorize]</c> would go, so when this is replaced by
/// real authentication the change is an attribute name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireBorrowerAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var currentBorrower = context.HttpContext.RequestServices.GetRequiredService<ICurrentBorrower>();
        var borrower = await currentBorrower.GetAsync(context.HttpContext.RequestAborted);

        if (borrower is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Not signed in",
                Detail = $"Send a known library member's address in the {HeaderCurrentBorrower.HeaderName} header."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };

            return;
        }

        await next();
    }
}
