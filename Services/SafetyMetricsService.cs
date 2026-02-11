using System.Diagnostics.Metrics;

namespace UserService.Services
{
    /// <summary>
    /// T071: Safety Metrics
    /// Tracks: Reports, Blocks, Moderation Queue, Safety Actions
    /// </summary>
    public class SafetyMetricsService
    {
        private static readonly Meter _meter = new("UserService.Safety", "1.0.0");

        // Report Metrics
        private static readonly Counter<long> _reportsSubmitted = _meter.CreateCounter<long>(
            "safety_reports_submitted_total",
            description: "Total safety reports submitted");

        private static readonly Counter<long> _reportsProcessed = _meter.CreateCounter<long>(
            "safety_reports_processed_total",
            description: "Reports that have been reviewed");

        private static readonly Histogram<double> _reportProcessingTime = _meter.CreateHistogram<double>(
            "safety_report_processing_time_hours",
            unit: "hours",
            description: "Time from report submission to resolution");

        // Block Metrics
        private static readonly Counter<long> _blocksCreated = _meter.CreateCounter<long>(
            "safety_blocks_created_total",
            description: "Total user blocks created");

        private static readonly Counter<long> _blocksRemoved = _meter.CreateCounter<long>(
            "safety_blocks_removed_total",
            description: "User blocks that were removed");

        private static long _activeBlocks = 0;
        private static readonly ObservableGauge<long> _activeBlocksGauge = _meter.CreateObservableGauge(
            "safety_blocks_active",
            () => _activeBlocks,
            description: "Current number of active user blocks");

        // Moderation Queue
        private static long _moderationQueueSize = 0;
        private static readonly ObservableGauge<long> _moderationQueueGauge = _meter.CreateObservableGauge(
            "safety_moderation_queue_size",
            () => _moderationQueueSize,
            description: "Number of items pending moderation review");

        private static readonly Counter<long> _moderationActions = _meter.CreateCounter<long>(
            "safety_moderation_actions_total",
            description: "Total moderation actions taken");

        // Safety Actions
        private static readonly Counter<long> _accountSuspensions = _meter.CreateCounter<long>(
            "safety_account_suspensions_total",
            description: "Accounts temporarily suspended");

        private static readonly Counter<long> _accountBans = _meter.CreateCounter<long>(
            "safety_account_bans_total",
            description: "Accounts permanently banned");

        private static readonly Counter<long> _contentRemoved = _meter.CreateCounter<long>(
            "safety_content_removed_total",
            description: "Content removed for policy violations");

        private static readonly Counter<long> _warningsIssued = _meter.CreateCounter<long>(
            "safety_warnings_issued_total",
            description: "Warnings issued to users");

        // Photo Moderation (from PhotoService)
        private static readonly Counter<long> _photosModerated = _meter.CreateCounter<long>(
            "safety_photos_moderated_total",
            description: "Photos reviewed by moderation");

        private static readonly Counter<long> _photosRejected = _meter.CreateCounter<long>(
            "safety_photos_rejected_total",
            description: "Photos rejected for policy violations");

        // Patterns and Trends
        private static readonly Counter<long> _repeatOffenders = _meter.CreateCounter<long>(
            "safety_repeat_offenders_total",
            description: "Users with multiple violations");

        private static readonly Histogram<int> _reportsPerUser = _meter.CreateHistogram<int>(
            "safety_reports_per_user",
            description: "Number of reports against a single user");

        // Public methods
        public void RecordReportSubmitted(string reportType, string severity = "medium")
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("type", reportType),
                new KeyValuePair<string, object?>("severity", severity)
            };
            _reportsSubmitted.Add(1, tags);
            Interlocked.Increment(ref _moderationQueueSize);
        }

        public void RecordReportProcessed(string reportType, string outcome, double processingHours)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("type", reportType),
                new KeyValuePair<string, object?>("outcome", outcome)
            };
            _reportsProcessed.Add(1, tags);
            _reportProcessingTime.Record(processingHours);
            Interlocked.Decrement(ref _moderationQueueSize);
        }

        public void RecordBlockCreated(string reason = "user_initiated")
        {
            var tags = new KeyValuePair<string, object?>("reason", reason);
            _blocksCreated.Add(1, tags);
            Interlocked.Increment(ref _activeBlocks);
        }

        public void RecordBlockRemoved()
        {
            _blocksRemoved.Add(1);
            Interlocked.Decrement(ref _activeBlocks);
        }

        public void RecordModerationAction(string actionType, string severity)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("action", actionType),
                new KeyValuePair<string, object?>("severity", severity)
            };
            _moderationActions.Add(1, tags);
        }

        public void RecordAccountSuspension(int durationDays, string reason)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("duration", durationDays.ToString()),
                new KeyValuePair<string, object?>("reason", reason)
            };
            _accountSuspensions.Add(1, tags);
        }

        public void RecordAccountBan(string reason)
        {
            var tags = new KeyValuePair<string, object?>("reason", reason);
            _accountBans.Add(1, tags);
        }

        public void RecordContentRemoved(string contentType, string reason)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("type", contentType),
                new KeyValuePair<string, object?>("reason", reason)
            };
            _contentRemoved.Add(1, tags);
        }

        public void RecordWarningIssued(string warningType)
        {
            var tags = new KeyValuePair<string, object?>("type", warningType);
            _warningsIssued.Add(1, tags);
        }

        public void RecordPhotoModeration(bool approved, string reason = "")
        {
            _photosModerated.Add(1);

            if (!approved)
            {
                var tags = new KeyValuePair<string, object?>("reason", string.IsNullOrEmpty(reason) ? "policy_violation" : reason);
                _photosRejected.Add(1, tags);
            }
        }

        public void RecordRepeatOffender(int violationCount)
        {
            _repeatOffenders.Add(1);
            _reportsPerUser.Record(violationCount);
        }

        public void UpdateModerationQueueSize(long size)
        {
            Interlocked.Exchange(ref _moderationQueueSize, size);
        }
    }
}
