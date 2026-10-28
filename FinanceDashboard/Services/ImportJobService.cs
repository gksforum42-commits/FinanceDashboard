using System.Collections.Concurrent;
using OfficeOpenXml;

namespace FinanceDashboard.Services
{
    /// <summary>
    /// Tracks long-running import jobs in memory so the HTTP request can return immediately
    /// and the browser can poll for progress. This prevents Azure/monsterASP.NET timeout
    /// from killing the import mid-way and sending the user back to the login page.
    /// </summary>
    public class ImportJobState
    {
        public string JobId         { get; set; } = string.Empty;
        public string Status        { get; set; } = "running";   // running | done | error
        public string Message       { get; set; } = string.Empty;
        public int    ProgressPct   { get; set; } = 0;
        public int    BERCount      { get; set; } = 0;
        public int    OriginalCount { get; set; } = 0;
        public List<string> Errors  { get; set; } = new();
        public DateTime StartedAt   { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
    }

    /// <summary>
    /// Holds file metadata between Preview and Import requests so we do NOT rely on
    /// session (which breaks on hosted servers / load-balanced environments).
    /// The browser receives a fileToken from Preview and sends it back with Import.
    /// </summary>
    public class PendingImport
    {
        public string TempPath      { get; set; } = string.Empty;
        public int    FinancialYear { get; set; }
        public string ImportType    { get; set; } = "Both";
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    }

    public class ImportJobService
    {
        private readonly ConcurrentDictionary<string, ImportJobState> _jobs    = new();
        private readonly ConcurrentDictionary<string, PendingImport>  _pending = new();

        // Caches the opened workbook per importId so each chunk request re-uses the same
        // in-memory package instead of re-parsing the entire XLSX from disk on every chunk
        // (re-parsing a large workbook on every request was spiking memory enough to trigger
        // OutOfMemoryException on constrained shared hosting).
        private readonly ConcurrentDictionary<long, ExcelPackage> _openWorkbooks = new();
        private readonly ConcurrentDictionary<long, DateTime> _openWorkbookTimestamps = new();

        public ExcelPackage GetOrOpenWorkbook(long importId, string filePath)
        {
            CleanupStaleWorkbooks();
            _openWorkbookTimestamps[importId] = DateTime.UtcNow;
            return _openWorkbooks.GetOrAdd(importId, _ => new ExcelPackage(new FileInfo(filePath)));
        }

        public void CloseWorkbook(long importId)
        {
            _openWorkbookTimestamps.TryRemove(importId, out _);
            if (_openWorkbooks.TryRemove(importId, out var package))
            {
                try { package.Dispose(); } catch { }
            }
        }

        /// <summary>Safety net: dispose any cached workbook that hasn't been touched in 30 minutes
        /// (e.g. the browser tab was closed mid-import so ImportFinalize was never called).</summary>
        private void CleanupStaleWorkbooks()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-30);
            foreach (var kv in _openWorkbookTimestamps)
                if (kv.Value < cutoff)
                    CloseWorkbook(kv.Key);
        }

        // ── Pending-import helpers (replaces session) ──────────────────────────

        public string StorePendingImport(string tempPath, int financialYear, string importType)
        {
            var token = Guid.NewGuid().ToString("N");
            _pending[token] = new PendingImport
            {
                TempPath      = tempPath,
                FinancialYear = financialYear,
                ImportType    = importType
            };
            // Clean up stale pending imports older than 30 minutes
            var cutoff = DateTime.UtcNow.AddMinutes(-30);
            foreach (var kv in _pending)
                if (kv.Value.CreatedAt < cutoff)
                    _pending.TryRemove(kv.Key, out _);
            return token;
        }

        public PendingImport? ConsumePendingImport(string token)
        {
            _pending.TryRemove(token, out var p);
            return p;
        }

        // ── Job-state helpers ──────────────────────────────────────────────────

        public ImportJobState Create(string jobId)
        {
            var state = new ImportJobState { JobId = jobId, Status = "running", ProgressPct = 5, Message = "Import starting…" };
            _jobs[jobId] = state;
            return state;
        }

        public ImportJobState? Get(string jobId)
            => _jobs.TryGetValue(jobId, out var s) ? s : null;

        public void Update(string jobId, Action<ImportJobState> updater)
        {
            if (_jobs.TryGetValue(jobId, out var s)) updater(s);
        }

        public void Complete(string jobId, int berCount, int originalCount, List<string> errors)
        {
            if (_jobs.TryGetValue(jobId, out var s))
            {
                s.Status        = errors.Count > 0 && berCount == 0 ? "error" : "done";
                s.ProgressPct   = 100;
                s.BERCount      = berCount;
                s.OriginalCount = originalCount;
                s.Errors        = errors;
                s.Message       = errors.Count > 0 && berCount == 0
                    ? $"Import failed: {errors.FirstOrDefault()}"
                    : $"Import completed. BER: {berCount}" + (originalCount > 0 ? $", OB: {originalCount}" : "");
                s.FinishedAt    = DateTime.UtcNow;
            }
        }

        public void Fail(string jobId, string errorMessage)
        {
            if (_jobs.TryGetValue(jobId, out var s))
            {
                s.Status      = "error";
                s.Message     = errorMessage;
                s.ProgressPct = 0;
                s.Errors      = new List<string> { errorMessage };
                s.FinishedAt  = DateTime.UtcNow;
            }
        }

        /// <summary>Remove finished jobs older than 2 hours to avoid unbounded memory growth.</summary>
        public void Cleanup()
        {
            var cutoff = DateTime.UtcNow.AddHours(-2);
            foreach (var kv in _jobs)
                if (kv.Value.FinishedAt.HasValue && kv.Value.FinishedAt < cutoff)
                    _jobs.TryRemove(kv.Key, out _);

            CleanupStaleWorkbooks();
        }
    }
}
