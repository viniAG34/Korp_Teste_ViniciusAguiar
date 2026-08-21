namespace Korp.Identity.Application.Authentication;

public sealed class IdentityServiceUnavailableException(Exception innerException)
    : Exception("Identity service is unavailable.", innerException);
