namespace FinSight.Application.Common.Exceptions;

public sealed class ConcurrencyConflictException()
    : Exception("The resource was changed by another request. Reload it and try again.");
