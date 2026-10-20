using FluentValidation;

namespace Paragrafen.Api.Filters;

public sealed class ValidationFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        if (context.Arguments.OfType<T>().FirstOrDefault() is not { } request)
        {
            throw new InvalidOperationException(
                $"{nameof(ValidationFilter<T>)}<{typeof(T).Name}> is applied to an endpoint "
                + $"that takes no {typeof(T).Name} argument."
            );
        }

        var validator =
            context.HttpContext.RequestServices.GetService<IValidator<T>>()
            ?? throw new InvalidOperationException(
                $"No IValidator<{typeof(T).Name}> is registered. Add one in "
                + "Paragrafen.Application/Validation — AddValidatorsFromAssembly picks it up."
            );

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }

        return await next(context);
    }
}
