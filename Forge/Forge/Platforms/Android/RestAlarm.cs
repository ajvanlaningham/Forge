using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Forge.Services.Interfaces;
using Application = Android.App.Application;

namespace Forge.Platforms.Android
{
    /// <summary>
    /// Rest-timer alert via AlarmManager. An exact alarm wakes a receiver that posts a
    /// high-importance, vibrating notification — works with the screen off and the app killed.
    /// </summary>
    /// <remarks>
    /// Exact alarms need USE_EXACT_ALARM (declared in the manifest). Google Play restricts that
    /// permission to alarm/timer apps, which is irrelevant here: Forge is sideloaded. If exact
    /// alarms are ever unavailable it falls back to an inexact one, which can be late by a little.
    /// </remarks>
    public sealed class RestAlarm : IRestAlarm
    {
        internal const string ChannelId = "rest_timer";
        internal const int NotificationId = 4201;
        private const int RequestCode = 4201;
        internal const string ExtraMessage = "message";

        private static Context Ctx => Application.Context;

        public bool CanNotify =>
            NotificationManagerCompat.From(Ctx).AreNotificationsEnabled();

        public async Task<bool> EnsurePermissionAsync()
        {
            EnsureChannel();
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                    status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted) return false;
            }
            return CanNotify;
        }

        public void Schedule(DateTime endsAtUtc, string message)
        {
            EnsureChannel();
            var alarms = (AlarmManager)Ctx.GetSystemService(Context.AlarmService)!;
            var pending = BuildPending(message);
            var triggerAt = new DateTimeOffset(endsAtUtc).ToUnixTimeMilliseconds();

            if (!OperatingSystem.IsAndroidVersionAtLeast(31) || alarms.CanScheduleExactAlarms())
                alarms.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pending);
            else
                alarms.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pending);
        }

        public void Cancel()
        {
            var alarms = (AlarmManager)Ctx.GetSystemService(Context.AlarmService)!;
            alarms.Cancel(BuildPending(""));
            NotificationManagerCompat.From(Ctx).Cancel(NotificationId);
        }

        private static PendingIntent BuildPending(string message)
        {
            var intent = new Intent(Ctx, typeof(RestAlarmReceiver));
            intent.PutExtra(ExtraMessage, message);
            return PendingIntent.GetBroadcast(Ctx, RequestCode, intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        }

        internal static void EnsureChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
            var manager = (NotificationManager)Ctx.GetSystemService(Context.NotificationService)!;
            if (manager.GetNotificationChannel(ChannelId) is not null) return;

            var channel = new NotificationChannel(ChannelId, "Rest timer", NotificationImportance.High)
            {
                Description = "Tells you when rest between sets is over."
            };
            channel.EnableVibration(true);
            channel.SetVibrationPattern(new long[] { 0, 350, 150, 350 });
            manager.CreateNotificationChannel(channel);
        }
    }

    [BroadcastReceiver(Enabled = true, Exported = false)]
    public sealed class RestAlarmReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context is null) return;
            RestAlarm.EnsureChannel();

            // Tapping the notification brings the app back to the session.
            var open = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
            open?.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var contentIntent = open is null ? null
                : PendingIntent.GetActivity(context, 0, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var message = intent?.GetStringExtra(RestAlarm.ExtraMessage);
            var notification = new NotificationCompat.Builder(context, RestAlarm.ChannelId)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle("Rest over")
                .SetContentText(string.IsNullOrEmpty(message) ? "Time for the next set." : message)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetCategory(NotificationCompat.CategoryAlarm)
                .SetVibrate(new long[] { 0, 350, 150, 350 })
                .SetAutoCancel(true)
                .SetTimeoutAfter(60_000)
                .SetContentIntent(contentIntent)
                .Build();

            try
            {
                NotificationManagerCompat.From(context).Notify(RestAlarm.NotificationId, notification);
            }
            catch (Java.Lang.SecurityException)
            {
                // Notification permission revoked after scheduling: nothing to do.
            }
        }
    }
}
