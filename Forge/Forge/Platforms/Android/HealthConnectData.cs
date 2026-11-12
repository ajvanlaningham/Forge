using Android.Content;
using Android.Runtime;

using AndroidX.Health.Connect.Client;
using AndroidX.Health.Connect.Client.Records;
using AndroidX.Health.Connect.Client.Request;
using AndroidX.Health.Connect.Client.Response;
using AndroidX.Health.Connect.Client.Time;

using Forge.Constants;
using Forge.Services.Interfaces;

using Kotlin.Jvm;

namespace Forge.Platforms.Android
{
    /// <summary>
    /// Reads resting heart rate, sleep and steps from Health Connect. Read-only; Forge never
    /// writes health data. Whatever wrote it (Fitbit, a Pixel Watch, Google Fit, a Garmin via a
    /// bridge app) doesn't matter here.
    /// </summary>
    public sealed class HealthConnectData : IHealthData
    {
        public const string ReadSteps = "android.permission.health.READ_STEPS";
        public const string ReadSleep = "android.permission.health.READ_SLEEP";
        public const string ReadRestingHr = "android.permission.health.READ_RESTING_HEART_RATE";
        public static readonly string[] Permissions = { ReadRestingHr, ReadSleep, ReadSteps };

        // HealthConnectClient.SDK_* status codes.
        private const int SdkAvailable = 3;
        private const int SdkUpdateRequired = 2;

        private static Context Ctx => global::Android.App.Application.Context;

        public string StoreName => "Health Connect";

        public HealthAvailability Availability
        {
            get
            {
                try
                {
                    return HealthConnectClient.GetSdkStatus(Ctx) switch
                    {
                        SdkAvailable => HealthAvailability.Available,
                        SdkUpdateRequired => HealthAvailability.NeedsInstall,
                        _ => HealthAvailability.Unsupported,
                    };
                }
                catch { return HealthAvailability.Unsupported; }
            }
        }

        private static IHealthConnectClient Client => HealthConnectClient.GetOrCreate(Ctx);

        public async Task<IReadOnlySet<string>> GrantedAsync()
        {
            var result = await KotlinSuspend.CallAsync(c => Client.PermissionController.GetGrantedPermissions(c));
            var granted = new HashSet<string>();
            if (result is not null)
            {
                var it = result.JavaCast<Java.Util.ISet>()!.Iterator()!;
                while (it.HasNext) granted.Add(it.Next()!.ToString()!);
            }
            return granted;
        }

        public async Task<bool> HasAnyPermissionAsync()
        {
            if (Availability != HealthAvailability.Available) return false;
            try { return (await GrantedAsync()).Overlaps(Permissions); }
            catch { return false; }
        }

        public async Task<bool> RequestPermissionsAsync()
        {
            if (Availability != HealthAvailability.Available) return false;
            await MainThread.InvokeOnMainThreadAsync(() => MainActivity.RequestHealthPermissionsAsync(Permissions));
            return await HasAnyPermissionAsync();
        }

        public void OpenSettings()
        {
            var intent = Availability == HealthAvailability.NeedsInstall
                ? new Intent(Intent.ActionView, global::Android.Net.Uri.Parse("market://details?id=com.google.android.apps.healthdata"))
                : new Intent("androidx.health.ACTION_HEALTH_CONNECT_SETTINGS");
            intent.AddFlags(ActivityFlags.NewTask);
            try { Ctx.StartActivity(intent); } catch { /* no store, no settings: nothing to open */ }
        }

        public async Task<HealthDay> ReadDayAsync(DateOnly date)
        {
            var granted = await GrantedAsync();
            var zone = TimeZoneInfo.Local;
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var from = new DateTimeOffset(dayStart, zone.GetUtcOffset(dayStart));
            var to = new DateTimeOffset(dayStart.AddDays(1), zone.GetUtcOffset(dayStart.AddDays(1)));

            int? steps = granted.Contains(ReadSteps) ? await StepsAsync(from, to) : null;
            int? rhr = granted.Contains(ReadRestingHr) ? await RestingHrAsync(from, to) : null;
            int? sleep = null;
            if (granted.Contains(ReadSleep))
            {
                var (sFrom, sTo) = HealthMath.SleepWindow(date, zone);
                sleep = await SleepAsync(sFrom, sTo);
            }
            return new HealthDay(rhr, sleep, steps);
        }

        private static Java.Time.Instant Instant(DateTimeOffset t) => Java.Time.Instant.OfEpochMilli(t.ToUnixTimeMilliseconds())!;
        private static DateTimeOffset Offset(Java.Time.Instant t) => DateTimeOffset.FromUnixTimeMilliseconds(t.ToEpochMilli());
        private static TimeRangeFilter Between(DateTimeOffset a, DateTimeOffset b) => TimeRangeFilter.Between(Instant(a), Instant(b));
        private static Kotlin.Reflect.IKClass KClass<T>() => JvmClassMappingKt.GetKotlinClass(Java.Lang.Class.FromType(typeof(T)));

        /// <summary>Aggregated, so a phone and a watch counting the same walk aren't added together.</summary>
        private static async Task<int?> StepsAsync(DateTimeOffset from, DateTimeOffset to)
        {
            var request = new AggregateRequest(new List<AndroidX.Health.Connect.Client.Aggregate.AggregateMetric> { StepsRecord.CountTotal },
                Between(from, to), new List<AndroidX.Health.Connect.Client.Records.Metadata.DataOrigin>());
            var result = (await KotlinSuspend.CallAsync(c => Client.Aggregate(request, c)))
                .JavaCast<AndroidX.Health.Connect.Client.Aggregate.AggregationResult>();
            if (result is null || !result.Contains(StepsRecord.CountTotal)) return null;
            var value = result.Get(StepsRecord.CountTotal);
            return value is null ? null : (int)value.JavaCast<Java.Lang.Long>()!.LongValue();
        }

        private static async Task<IReadOnlyList<T>> ReadAsync<T>(DateTimeOffset from, DateTimeOffset to) where T : Java.Lang.Object
        {
            var records = new List<T>();
            string? page = null;
            do
            {
                var request = new ReadRecordsRequest(KClass<T>(), Between(from, to),
                    new List<AndroidX.Health.Connect.Client.Records.Metadata.DataOrigin>(), true, 1000, page);
                var response = (await KotlinSuspend.CallAsync(c => Client.ReadRecords(request, c))).JavaCast<ReadRecordsResponse>();
                if (response is null) break;
                foreach (var r in response.Records) records.Add(((Java.Lang.Object)r).JavaCast<T>()!);
                page = response.PageToken;
            } while (!string.IsNullOrEmpty(page));
            return records;
        }

        /// <summary>The day's most recent reading (sources usually write one per day).</summary>
        private static async Task<int?> RestingHrAsync(DateTimeOffset from, DateTimeOffset to)
        {
            var records = await ReadAsync<RestingHeartRateRecord>(from, to);
            var latest = records.OrderBy(r => r.Time.ToEpochMilli()).LastOrDefault();
            return latest is null ? null : (int)latest.BeatsPerMinute;
        }

        private static async Task<int?> SleepAsync(DateTimeOffset from, DateTimeOffset to)
        {
            var records = await ReadAsync<SleepSessionRecord>(from.AddHours(-18), to);
            var sessions = records
                .Where(r => Offset(r.EndTime) > from && Offset(r.EndTime) <= to)
                .Select(r => new SleepSession(Offset(r.StartTime), Offset(r.EndTime),
                    r.Stages.Select(s => new SleepStage(Offset(s.StartTime), Offset(s.EndTime), s.GetStage())).ToList()));
            return HealthMath.SleepMinutes(sessions);
        }
    }
}
