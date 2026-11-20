using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pancras.Application.Common;

namespace Pancras.Infrastructure.Persistence;

/// <summary>
/// Giao dịch tường minh trên <see cref="PancrasDbContext"/> của request (docs/02-coding-rules/data-access.md mục 3).
/// Repository chỉ đọc và thêm vào ngữ cảnh; <see cref="IUnitOfWorkScope.CommitAsync"/> lưu mọi thứ (kể cả nhật ký đã <c>Stage</c>) rồi commit.
/// </summary>
public sealed class EfUnitOfWork(PancrasDbContext context, ILogger<EfUnitOfWork> logger) : IUnitOfWork
{
    public async Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Đã có giao dịch đang mở; UnitOfWork không lồng nhau.");
        }

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new Scope(context, transaction, logger);
    }

    private sealed class Scope(PancrasDbContext context, IDbContextTransaction transaction, ILogger logger) : IUnitOfWorkScope
    {
        private bool _finished;

        public async Task<SaveOutcome> CommitAsync(CancellationToken cancellationToken)
        {
            if (_finished)
            {
                throw new InvalidOperationException("Scope đã kết thúc.");
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _finished = true;
                return SaveOutcome.Saved;
            }
            catch (DbUpdateConcurrencyException)
            {
                await AbortAsync();
                return SaveOutcome.Conflict;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                await AbortAsync();
                return SaveOutcome.Duplicate;
            }
            catch (DbUpdateException exception)
            {
                logger.LogError(exception, "Commit failed; the transaction was rolled back.");
                await AbortAsync();
                return SaveOutcome.Failed;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_finished)
            {
                await AbortAsync();
            }

            await transaction.DisposeAsync();
        }

        /// <summary>Hủy giao dịch và bỏ mọi thay đổi chưa lưu trong ngữ cảnh, để không có gì rò sang lần lưu sau trong cùng request.</summary>
        private async Task AbortAsync()
        {
            _finished = true;
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Rollback failed.");
            }
            finally
            {
                context.ChangeTracker.Clear();
            }
        }
    }
}
