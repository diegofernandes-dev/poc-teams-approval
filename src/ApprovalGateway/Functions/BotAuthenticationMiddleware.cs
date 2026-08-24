using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace ApprovalGateway.Functions;

public sealed class BotAuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (context.FunctionDefinition.Name != nameof(BotMessagesFunction.BotMessages))
        {
            await next(context);
            return;
        }

        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        var authService = httpContext.RequestServices.GetRequiredService<IAuthenticationService>();
        var result = await authService.AuthenticateAsync(httpContext, null);

        if (result?.Succeeded != true)
        {
            var response = httpContext.Response;
            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.GetInvocationResult().Value = new EmptyResult();
            return;
        }

        httpContext.User = result.Principal!;
        await next(context);
    }
}
