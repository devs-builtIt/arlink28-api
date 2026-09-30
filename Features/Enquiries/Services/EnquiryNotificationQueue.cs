using System.Threading.Channels;
using Arlink28.Api.Features.Enquiries.Services.Interfaces;
using Arlink28.Api.Features.Shared.Interfaces;

namespace Arlink28.Api.Features.Enquiries.Services;

/// <summary>An in-memory queue: if the API restarts with items waiting, those emails are not sent (the enquiries are still saved).</summary>
public class EnquiryNotificationQueue : IEnquiryNotificationQueue, ISingleton
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid enquiryId) => _channel.Writer.TryWrite(enquiryId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
