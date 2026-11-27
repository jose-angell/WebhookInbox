using WebhookInbox.Domain.Enums;
using WebhookInbox.Domain.Exceptions;
using WebhookInbox.Domain.Services;

namespace WebhookInbox.Tests.Domain;

public class InboxEventRegistrarTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (InboxEventRegistrar Registrar, InMemoryInboxEventRepository Repo) Create()
    {
        var repo = new InMemoryInboxEventRepository();
        return (new InboxEventRegistrar(repo, new FixedTimeProvider(Now)), repo);
    }

    [Fact]
    public async Task RegisterAsync_NewEventId_AddsOnceAndReturnsReceivedEvent()
    {
        var (registrar, repo) = Create();

        var e = await registrar.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        Assert.Equal(1, repo.AddCallCount);
        Assert.Same(e, Assert.Single(repo.Events));
        Assert.Equal(EventStatus.Received, e.Status);
        Assert.Equal(Now, e.ReceivedAt);
        Assert.Equal("evt_1", e.EventId);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEventId_ThrowsDuplicateAndDoesNotAdd()
    {
        var (registrar, repo) = Create();
        await registrar.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateEventException>(() =>
            registrar.RegisterAsync("evt_1", "github", "push", "{}", CancellationToken.None));

        Assert.Equal("evt_1", ex.EventId);
        Assert.Equal(1, repo.AddCallCount);
        Assert.Single(repo.Events);
    }

    [Fact]
    public async Task RegisterAsync_EventIdComparisonIsExact()
    {
        var (registrar, repo) = Create();
        await registrar.RegisterAsync("evt_1", "stripe", "t", "{}", CancellationToken.None);

        await registrar.RegisterAsync("EVT_1", "stripe", "t", "{}", CancellationToken.None);
        await registrar.RegisterAsync("evt_1 ", "stripe", "t", "{}", CancellationToken.None);

        Assert.Equal(3, repo.AddCallCount);
    }

    [Fact]
    public async Task RegisterAsync_InvalidArguments_ThrowsAndDoesNotAdd()
    {
        var (registrar, repo) = Create();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            registrar.RegisterAsync("", "stripe", "t", "{}", CancellationToken.None));

        Assert.Equal(0, repo.AddCallCount);
    }
}
