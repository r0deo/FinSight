namespace FinSight.Application.Common.Exceptions;

public sealed class NotFoundException(string entityName, object key)
    : Exception($"{entityName} with identifier '{key}' was not found.");
