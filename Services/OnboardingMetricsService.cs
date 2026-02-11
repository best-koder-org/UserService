using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace UserService.Services
{
    /// <summary>
    /// T068: Onboarding Funnel Metrics
    /// Tracks: Registration → Profile Creation → Photo Upload → Account Activation
    /// </summary>
    public class OnboardingMetricsService
    {
        private static readonly Meter _meter = new("UserService.Onboarding", "1.0.0");

        // Counters
        private static readonly Counter<long> _registrationStarted = _meter.CreateCounter<long>(
            "onboarding_registration_started_total",
            description: "Total number of registration attempts");

        private static readonly Counter<long> _registrationCompleted = _meter.CreateCounter<long>(
            "onboarding_registration_completed_total",
            description: "Total number of successful registrations");

        private static readonly Counter<long> _profileCreated = _meter.CreateCounter<long>(
            "onboarding_profile_created_total",
            description: "Total number of profiles created");

        private static readonly Counter<long> _wizardStepCompleted = _meter.CreateCounter<long>(
            "onboarding_wizard_step_completed_total",
            description: "Wizard steps completed by step number");

        private static readonly Counter<long> _photoUploaded = _meter.CreateCounter<long>(
            "onboarding_photo_uploaded_total",
            description: "Total photos uploaded during onboarding");

        private static readonly Counter<long> _onboardingCompleted = _meter.CreateCounter<long>(
            "onboarding_completed_total",
            description: "Total users who completed full onboarding");

        private static readonly Counter<long> _onboardingAbandoned = _meter.CreateCounter<long>(
            "onboarding_abandoned_total",
            description: "Users who abandoned onboarding process");

        // Histograms for timing
        private static readonly Histogram<double> _onboardingDuration = _meter.CreateHistogram<double>(
            "onboarding_duration_seconds",
            unit: "seconds",
            description: "Time to complete onboarding from registration to activation");

        private static readonly Histogram<double> _wizardStepDuration = _meter.CreateHistogram<double>(
            "onboarding_wizard_step_duration_seconds",
            unit: "seconds",
            description: "Time to complete each wizard step");

        // Gauges (via ObservableGauge)
        private static long _activeOnboardingUsers = 0;
        private static readonly ObservableGauge<long> _activeOnboardingGauge = _meter.CreateObservableGauge(
            "onboarding_users_active",
            () => _activeOnboardingUsers,
            description: "Current number of users in onboarding process");

        // Public methods to record metrics
        public void RecordRegistrationStarted() =>
            _registrationStarted.Add(1, new KeyValuePair<string, object?>("source", "keycloak"));

        public void RecordRegistrationCompleted(string method = "email") =>
            _registrationCompleted.Add(1, new KeyValuePair<string, object?>("method", method));

        public void RecordProfileCreated() =>
            _profileCreated.Add(1);

        public void RecordWizardStepCompleted(int stepNumber)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("step", stepNumber.ToString()),
                new KeyValuePair<string, object?>("step_name", GetStepName(stepNumber))
            };
            _wizardStepCompleted.Add(1, tags);
        }

        public void RecordWizardStepDuration(int stepNumber, double seconds)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("step", stepNumber.ToString())
            };
            _wizardStepDuration.Record(seconds, tags);
        }

        public void RecordPhotoUploaded(bool isPrimary = false) =>
            _photoUploaded.Add(1, new KeyValuePair<string, object?>("is_primary", isPrimary));

        public void RecordOnboardingCompleted(double totalSeconds)
        {
            _onboardingCompleted.Add(1);
            _onboardingDuration.Record(totalSeconds);
            Interlocked.Decrement(ref _activeOnboardingUsers);
        }

        public void RecordOnboardingAbandoned(int lastStepReached)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("last_step", lastStepReached.ToString())
            };
            _onboardingAbandoned.Add(1, tags);
            Interlocked.Decrement(ref _activeOnboardingUsers);
        }

        public void RecordActiveOnboardingUser(bool isActive)
        {
            if (isActive)
                Interlocked.Increment(ref _activeOnboardingUsers);
            else
                Interlocked.Decrement(ref _activeOnboardingUsers);
        }

        private static string GetStepName(int stepNumber) => stepNumber switch
        {
            1 => "BasicInfo",
            2 => "Preferences",
            3 => "Photos",
            _ => "Unknown"
        };
    }
}
