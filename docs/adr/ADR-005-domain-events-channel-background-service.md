# ADR-005: Domain Events — Dispatch Síncrono Direto nos Use Cases

**Status:** Superseded (intenção original: Channel<T> + BackgroundService; decisão real: dispatch síncrono)
**Data original:** 2025-05-01  
**Atualizado em:** 2026-05-28

## Contexto

Domain events (`TicketCreated`, `PriorityChanged`, `SlaBreached`, etc.) precisam disparar side effects — audit logging e dispatch de notificações — sem acoplar o aggregate à infraestrutura.

## Decisão Original (não implementada)

O ADR original documentava `Channel<IDomainEvent>` + `BackgroundService` + `IDomainEventHandler<T>`. Essa arquitetura não foi implementada.

## Decisão Real

Após `SaveChangesAsync`, cada Use Case itera `aggregate.DomainEvents` e chama `auditService.RecordAsync` e `notifications.Notify*Async` diretamente, de forma síncrona:

```csharp
await repository.SaveChangesAsync(ct);
foreach (var evt in ticket.DomainEvents)
    await auditService.RecordAsync(evt.GetType().Name, "Ticket", ticket.Id, actorId, evt, ct);
ticket.ClearDomainEvents();
await notifications.NotifyTicketResolvedAsync(...);
```

O `SlaBreachMonitorService` segue o mesmo padrão via `ProcessSlaBreachesUseCase`.

## Consequências

**Vantagens do dispatch síncrono:**
- Simples, sem dependências extras
- Observable: falhas de audit são visíveis na resposta HTTP
- Zero event loss entre SaveChanges e dispatch (tudo na mesma request)

**Desvantagens:**
- Audit failures afetam a resposta HTTP (latência de N saves adicionais)
- Cada `auditService.RecordAsync` abre um novo DbContext scope — audit e estado do aggregate são salvos em transações separadas. Se o processo cair entre eles, o estado persiste mas o audit event se perde.
- Sem suporte a retry para eventos individuais

## Tradeoff Aceito

Para um portfolio project, simplicidade prevalece. O `IAuditService` ser singleton com `IServiceScopeFactory` já resolve o captive dependency problem.

## Caminho de Migração (se necessário)

Para produção com requisitos de entrega garantida:
1. Adicionar tabela `outbox_events` (id, event_type, payload, occurred_at, processed_at)
2. Salvar eventos na mesma transação que o aggregate (`SaveChangesAsync` único)
3. `BackgroundService` processa outbox em background com retry/dead-letter
