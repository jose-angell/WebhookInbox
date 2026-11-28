using WebhookInbox.Application.Abstractions;
using WebhookInbox.Application.Dtos;
using WebhookInbox.Application.Services;
using WebhookInbox.Domain.Enums;
using WebhookInbox.Domain.Exceptions;
using WebhookInbox.Domain.Services;
using WebhookInbox.Tests.Domain;

namespace WebhookInbox.Tests.Application;

public class InboxEventServiceTests
{
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset Start = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private static (InboxEventService Service, InMemoryInboxEventRepository Repo, MutableTimeProvider Time) Create() =>
        Create(new FakeInboxEventProcessor());

    private static (InboxEventService Service, InMemoryInboxEventRepository Repo, MutableTimeProvider Time) Create(
        IInboxEventProcessor processor)
    {
        var repo = new InMemoryInboxEventRepository();
        var time = new MutableTimeProvider(Start);
        var service = new InboxEventService(new InboxEventRegistrar(repo, time), repo, processor, time);
        return (service, repo, time);
    }

    private static async Task RegisterAt(InboxEventService service, MutableTimeProvider time,
                                         string eventId, TimeSpan offset)
    {
        time.Now = Start + offset;
        await service.RegisterAsync(eventId, "stripe", "invoice.paid", "{}", CancellationToken.None);
    }

    [Fact]
    public async Task RegisterAsync_NewEventId_ReturnsReceivedDto()
    {
        var (service, repo, _) = Create();

        var dto = await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        Assert.Equal(EventStatus.Received, dto.Status);
        Assert.Equal("evt_1", dto.EventId);
        Assert.Equal(Start, dto.ReceivedAt);
        Assert.Single(repo.Events);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEventId_ThrowsDuplicateEventException()
    {
        var (service, _, _) = Create();
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateEventException>(() =>
            service.RegisterAsync("evt_1", "github", "push", "{}", CancellationToken.None));
    }

    [Fact]
    public async Task GetByEventIdAsync_Existing_ReturnsDto()
    {
        var (service, _, _) = Create();
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        var dto = await service.GetByEventIdAsync("evt_1", CancellationToken.None);

        Assert.Equal("evt_1", dto.EventId);
        Assert.Equal("stripe", dto.Source);
    }

    [Fact]
    public async Task GetByEventIdAsync_Missing_ThrowsEventNotFoundException()
    {
        var (service, _, _) = Create();

        var ex = await Assert.ThrowsAsync<EventNotFoundException>(() =>
            service.GetByEventIdAsync("nope", CancellationToken.None));

        Assert.Equal("nope", ex.EventId);
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_ReturnsOnlyThatStatusNewestFirst()
    {
        var (service, repo, time) = Create();
        await RegisterAt(service, time, "evt_1", TimeSpan.Zero);
        await RegisterAt(service, time, "evt_2", TimeSpan.FromMinutes(1));
        await RegisterAt(service, time, "evt_3", TimeSpan.FromMinutes(2));
        await RegisterAt(service, time, "evt_4", TimeSpan.FromMinutes(3));

        foreach (var id in new[] { "evt_1", "evt_3" })
        {
            var e = repo.Events.Single(x => x.EventId == id);
            e.StartProcessing();
            e.MarkFailed("boom");
        }

        var result = await service.ListAsync(EventStatus.Failed, 1, 20, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, i => Assert.Equal(EventStatus.Failed, i.Status));
        Assert.Equal(["evt_3", "evt_1"], result.Items.Select(i => i.EventId));
    }

    [Fact]
    public async Task ListAsync_NullStatus_ReturnsAllEvents()
    {
        var (service, _, time) = Create();
        await RegisterAt(service, time, "evt_1", TimeSpan.Zero);
        await RegisterAt(service, time, "evt_2", TimeSpan.FromMinutes(1));

        var result = await service.ListAsync(null, 1, 20, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task ListAsync_Pagination_ReturnsRequestedPageWithCorrectTotal()
    {
        var (service, _, time) = Create();
        for (var i = 1; i <= 5; i++)
            await RegisterAt(service, time, $"evt_{i}", TimeSpan.FromMinutes(i));

        var page2 = await service.ListAsync(null, 2, 2, CancellationToken.None);

        Assert.Equal(2, page2.Page);
        Assert.Equal(2, page2.PageSize);
        Assert.Equal(5, page2.Total);
        Assert.Equal(["evt_3", "evt_2"], page2.Items.Select(x => x.EventId));

        var page3 = await service.ListAsync(null, 3, 2, CancellationToken.None);
        Assert.Equal(["evt_1"], page3.Items.Select(x => x.EventId));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1, -1)]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    public async Task ListAsync_OutOfRange_ThrowsArgumentOutOfRangeException(int page, int pageSize)
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ListAsync(null, page, pageSize, CancellationToken.None));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task ListAsync_PageSizeBoundaries_AreAccepted(int pageSize)
    {
        var (service, _, _) = Create();

        var result = await service.ListAsync(null, 1, pageSize, CancellationToken.None);

        Assert.Equal(pageSize, result.PageSize);
    }

    [Fact]
    public async Task ProcessAsync_ProcessorSucceeds_ReturnsProcessedWithTimeProviderTime()
    {
        var processor = new FakeInboxEventProcessor();
        var (service, repo, time) = Create(processor);
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);
        var processedAt = Start.AddMinutes(5);
        time.Now = processedAt;

        var dto = await service.ProcessAsync("evt_1", CancellationToken.None);

        Assert.Equal(EventStatus.Processed, dto.Status);
        Assert.Equal(processedAt, dto.ProcessedAt);
        Assert.Equal(1, dto.AttemptCount);
        Assert.Equal(1, processor.CallCount);
        Assert.Equal(EventStatus.Processing, processor.LastReceived!.Status);
        Assert.Equal(2, repo.UpdateCallCount);
    }

    [Fact]
    public async Task ProcessAsync_ProcessorThrows_ReturnsFailedWithLastErrorAndDoesNotRethrow()
    {
        var processor = new FakeInboxEventProcessor { ExceptionToThrow = new InvalidOperationException("boom") };
        var (service, repo, _) = Create(processor);
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        var dto = await service.ProcessAsync("evt_1", CancellationToken.None);

        Assert.Equal(EventStatus.Failed, dto.Status);
        Assert.Equal("boom", dto.LastError);
        Assert.Null(dto.ProcessedAt);
        Assert.Equal(2, repo.UpdateCallCount);
    }

    [Fact]
    public async Task ProcessAsync_ProcessorThrowsWithEmptyMessage_StillMarksFailed()
    {
        var processor = new FakeInboxEventProcessor { ExceptionToThrow = new InvalidOperationException("") };
        var (service, _, _) = Create(processor);
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        var dto = await service.ProcessAsync("evt_1", CancellationToken.None);

        Assert.Equal(EventStatus.Failed, dto.Status);
        Assert.Equal(nameof(InvalidOperationException), dto.LastError);
    }

    [Fact]
    public async Task ProcessAsync_FromFailed_RetriesAndAttemptCountBecomesTwo()
    {
        var processor = new FakeInboxEventProcessor { ExceptionToThrow = new InvalidOperationException("boom") };
        var (service, _, _) = Create(processor);
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);
        await service.ProcessAsync("evt_1", CancellationToken.None);
        processor.ExceptionToThrow = null;

        var dto = await service.ProcessAsync("evt_1", CancellationToken.None);

        Assert.Equal(EventStatus.Processed, dto.Status);
        Assert.Equal(2, dto.AttemptCount);
        Assert.Null(dto.LastError);
        Assert.Equal(2, processor.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_AlreadyProcessed_ThrowsInvalidTransitionWithoutCallingProcessor()
    {
        var processor = new FakeInboxEventProcessor();
        var (service, _, _) = Create(processor);
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);
        await service.ProcessAsync("evt_1", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidEventTransitionException>(() =>
            service.ProcessAsync("evt_1", CancellationToken.None));

        Assert.Equal(1, processor.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_AlreadyProcessing_ThrowsInvalidTransitionWithoutCallingProcessor()
    {
        var processor = new FakeInboxEventProcessor();
        var (service, repo, _) = Create(processor);
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);
        repo.Events.Single().StartProcessing();

        await Assert.ThrowsAsync<InvalidEventTransitionException>(() =>
            service.ProcessAsync("evt_1", CancellationToken.None));

        Assert.Equal(0, processor.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_Missing_ThrowsEventNotFoundException()
    {
        var processor = new FakeInboxEventProcessor();
        var (service, _, _) = Create(processor);

        var ex = await Assert.ThrowsAsync<EventNotFoundException>(() =>
            service.ProcessAsync("nope", CancellationToken.None));

        Assert.Equal("nope", ex.EventId);
        Assert.Equal(0, processor.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_ProcessorCancelled_RethrowsAndDoesNotMarkFailed()
    {
        var processor = new FakeInboxEventProcessor { ExceptionToThrow = new OperationCanceledException() };
        var (service, repo, _) = Create(processor);
        await service.RegisterAsync("evt_1", "stripe", "invoice.paid", "{}", CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ProcessAsync("evt_1", CancellationToken.None));

        Assert.Equal(EventStatus.Processing, repo.Events.Single().Status);
    }
}
