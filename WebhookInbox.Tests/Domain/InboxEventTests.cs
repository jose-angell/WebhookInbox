using WebhookInbox.Domain.Entities;
using WebhookInbox.Domain.Enums;

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
}
