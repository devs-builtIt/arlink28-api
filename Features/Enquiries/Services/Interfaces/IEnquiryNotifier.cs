namespace Arlink28.Api.Features.Enquiries.Services.Interfaces;

/// <summary>Tells staff by email about an enquiry that is already saved.</summary>
public interface IEnquiryNotifier
{
    /// <summary>
    /// Sends the notification for a saved enquiry. Does nothing when no address is configured or the
    /// enquiry is gone. A mail problem is thrown, for the caller to retry or log.
    /// </summary>
    Task SendAsync(Guid enquiryId, CancellationToken ct = default);
}

/// <summary>
/// Enquiries waiting to be announced. The guest's request only adds to the queue and returns; a
/// background worker does the slow part (the photo, the mail server), so a guest never waits on email.
/// </summary>
public interface IEnquiryNotificationQueue
{
    void Enqueue(Guid enquiryId);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
}
