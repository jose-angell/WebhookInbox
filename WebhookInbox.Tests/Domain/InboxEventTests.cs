using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Enums;
using WebhookInbox.Domain.Exceptions;

namespace WebhookInbox.Tests.Domain;

public class InboxEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receive_SetsInitialState()
    {
        var e = InboxEvent.Receive("evt_1", "stripe", "invoice.paid", "{\"a\":1}", Now);

        Assert.NotEqual(Guid.Empty, e.Id);
        Assert.Equal("evt_1", e.EventId);
        Assert.Equal("stripe", e.Source);
        Assert.Equal("invoice.paid", e.EventType);
        Assert.Equal("{\"a\":1}", e.Payload);
        Assert.Equal(EventStatus.Received, e.Status);
        Assert.Equal(Now, e.ReceivedAt);
        Assert.Equal(0, e.AttemptCount);
        Assert.Null(e.ProcessedAt);
        Assert.Null(e.LastError);
    }

    [Fact]
    public void Receive_GeneratesDistinctIds()
    {
        var a = InboxEvent.Receive("evt_1", "stripe", "t", "{}", Now);
        var b = InboxEvent.Receive("evt_2", "stripe", "t", "{}", Now);

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Receive_AcceptsEmptyPayload()
    {
        var e = InboxEvent.Receive("evt_1", "stripe", "t", "", Now);

        Assert.Equal("", e.Payload);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Receive_InvalidEventId_Throws(string? eventId) =>
        Assert.ThrowsAny<ArgumentException>(() => InboxEvent.Receive(eventId!, "stripe", "t", "{}", Now));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Receive_InvalidSource_Throws(string? source) =>
        Assert.ThrowsAny<ArgumentException>(() => InboxEvent.Receive("evt_1", source!, "t", "{}", Now));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Receive_InvalidEventType_Throws(string? eventType) =>
        Assert.ThrowsAny<ArgumentException>(() => InboxEvent.Receive("evt_1", "stripe", eventType!, "{}", Now));

    [Fact]
    public void Receive_NullPayload_Throws() =>
        Assert.ThrowsAny<ArgumentException>(() => InboxEvent.Receive("evt_1", "stripe", "t", null!, Now));

    private static InboxEvent NewEvent() => InboxEvent.Receive("evt_1", "stripe", "t", "{}", Now);

    private static InboxEvent Processing()
    {
        var e = NewEvent();
        e.StartProcessing();
        return e;
    }

    private static InboxEvent Failed()
    {
        var e = Processing();
        e.MarkFailed("boom");
        return e;
    }

    private static InboxEvent Processed()
    {
        var e = Processing();
        e.MarkProcessed(Now);
        return e;
    }

    // Valid transitions

    [Fact]
    public void StartProcessing_FromReceived_MovesToProcessingAndIncrementsAttempt()
    {
        var e = NewEvent();

        e.StartProcessing();

        Assert.Equal(EventStatus.Processing, e.Status);
        Assert.Equal(1, e.AttemptCount);
    }

    [Fact]
    public void StartProcessing_FromFailed_RetriesAndIncrementsAttempt()
    {
        var e = Failed();
        Assert.Equal(1, e.AttemptCount);

        e.StartProcessing();

        Assert.Equal(EventStatus.Processing, e.Status);
        Assert.Equal(2, e.AttemptCount);
    }

    [Fact]
    public void MarkProcessed_FromProcessing_SetsProcessedAndClearsError()
    {
        var e = Failed();
        e.StartProcessing();
        var at = Now.AddMinutes(5);

        e.MarkProcessed(at);

        Assert.Equal(EventStatus.Processed, e.Status);
        Assert.Equal(at, e.ProcessedAt);
        Assert.Null(e.LastError);
    }

    [Fact]
    public void MarkFailed_FromProcessing_SetsFailedAndError()
    {
        var e = Processing();

        e.MarkFailed("timeout");

        Assert.Equal(EventStatus.Failed, e.Status);
        Assert.Equal("timeout", e.LastError);
    }

    // Invalid transitions

    [Fact]
    public void StartProcessing_FromProcessed_Throws()
    {
        var e = Processed();

        var ex = Assert.Throws<InvalidEventTransitionException>(() => e.StartProcessing());

        Assert.Equal(EventStatus.Processed, ex.CurrentStatus);
        Assert.Equal(EventStatus.Processed, e.Status);
        Assert.Equal(1, e.AttemptCount);
    }

    [Fact]
    public void StartProcessing_FromProcessing_Throws() =>
        Assert.Throws<InvalidEventTransitionException>(() => Processing().StartProcessing());

    [Fact]
    public void MarkProcessed_FromReceived_Throws() =>
        Assert.Throws<InvalidEventTransitionException>(() => NewEvent().MarkProcessed(Now));

    [Fact]
    public void MarkProcessed_FromFailed_Throws() =>
        Assert.Throws<InvalidEventTransitionException>(() => Failed().MarkProcessed(Now));

    [Fact]
    public void MarkProcessed_FromProcessed_Throws() =>
        Assert.Throws<InvalidEventTransitionException>(() => Processed().MarkProcessed(Now));

    [Fact]
    public void MarkFailed_FromReceived_Throws() =>
        Assert.Throws<InvalidEventTransitionException>(() => NewEvent().MarkFailed("x"));

    [Fact]
    public void MarkFailed_FromFailed_Throws() =>
        Assert.Throws<InvalidEventTransitionException>(() => Failed().MarkFailed("x"));

    [Fact]
    public void MarkFailed_FromProcessed_Throws() =>
        Assert.Throws<InvalidEventTransitionException>(() => Processed().MarkFailed("x"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MarkFailed_InvalidError_Throws(string? error) =>
        Assert.ThrowsAny<ArgumentException>(() => Processing().MarkFailed(error!));
}
