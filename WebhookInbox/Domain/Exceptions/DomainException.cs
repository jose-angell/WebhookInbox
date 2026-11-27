namespace WebhookInbox.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message);
