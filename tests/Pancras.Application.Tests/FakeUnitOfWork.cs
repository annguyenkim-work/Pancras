using Pancras.Application.Common;

namespace Pancras.Application.Tests;

/// <summary>Scope giả cho test handler: <c>CommitAsync</c> gọi hàm do fake store cung cấp (lưu phần staging, trả kết quả cấu hình sẵn).</summary>
internal sealed class FakeUnitOfWorkScope(Func<SaveOutcome> commit, Action? onDispose = null) : IUnitOfWorkScope
{
    public bool Committed { get; private set; }

    public Task<SaveOutcome> CommitAsync(CancellationToken cancellationToken)
    {
        var outcome = commit();
        Committed = outcome == SaveOutcome.Saved;
        return Task.FromResult(outcome);
    }

    public ValueTask DisposeAsync()
    {
        onDispose?.Invoke();
        return ValueTask.CompletedTask;
    }
}

/// <summary>UnitOfWork giả luôn lưu được, cho test không quan tâm tới kết quả lưu.</summary>
internal sealed class NoopUnitOfWork : IUnitOfWork
{
    public Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IUnitOfWorkScope>(new FakeUnitOfWorkScope(() => SaveOutcome.Saved));
}
