using Beacon.Application.Common.Exceptions;
using FluentValidation;
using MediatR;
using ValidationException = Beacon.Application.Common.Exceptions.ValidationException;

namespace Beacon.Application.Common.Behaviors
{
    #region ValidationBehavior la gi?
    /*
     * Nó là một Pipeline ( middleware nội bộ của MediatR
     * Mọi request(Command/Query) đi qua đây trước khi vào Handler
     * 
     * MediatR là một thư viện giúp triển khai pattern
     * Thay vì: Controller -> gọi Service -> gọi Repository
     * thì: Controller -> MediatR -> Handler 
     * MediatR đóng vai trò " Người trung gian điều phối request"
     */
    #endregion
    public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!validators.Any())
                return await next();

            var validationResults = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(request, cancellationToken)));

            var errors = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .Select(f => f.ErrorMessage)
                .ToList();

            if (errors.Count > 0)
                throw new ValidationException(errors);

            return await next();
        }
    }
}
