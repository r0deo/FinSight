namespace FinSight.Application.Common.Exceptions;

public sealed class ForbiddenAccessException(string message) : Exception(message);
