namespace Korp.Identity.Application.Authentication;

public sealed record LoginCommand(string Email, string Password);
