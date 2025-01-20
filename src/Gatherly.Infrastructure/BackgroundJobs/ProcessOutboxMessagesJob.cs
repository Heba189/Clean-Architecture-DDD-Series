using Gatherly.Domain.Primitives;
using Gatherly.Persistence;
using Gatherly.Persistence.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Quartz;

namespace Gatherly.Infrastructure.BackgroundJobs;
public class ProcessOutboxMessagesJob : IJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPublisher _publisher;
    public ProcessOutboxMessagesJob(ApplicationDbContext dbContext, IPublisher publisher)
    {
        _dbContext = dbContext;
        _publisher = publisher;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        List<OutboxMessage> outboxMessages = await _dbContext
            .Set<OutboxMessage>()
            .Where(x => x.ProcessedOnUtc == null)
            .Take(20)
            .ToListAsync(context.CancellationToken);

        foreach (OutboxMessage outboxMessage in outboxMessages)
        {
            IDomainEvent? domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(
                outboxMessage.Content);
            if (domainEvent is null)
            {
                continue;
            }
            await _publisher.Publish(domainEvent, context.CancellationToken);
            outboxMessage.ProcessedOnUtc = DateTime.UtcNow;
        }
        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
