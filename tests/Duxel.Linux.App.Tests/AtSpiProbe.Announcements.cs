using System.Runtime.InteropServices;
using System.Text.Json;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunAnnouncements(uint parentPid, string output, bool automatic = false)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh native announcement result file.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Announcement AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        var checks = new List<string>();
        var captured = Array.Empty<NativeAnnouncementEvent>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
        }
        try
        {
            using var app = OwnedApplication(parentPid);
            atspi_accessible_set_cache_mask(app.Value, 0);
            using var window = Child(app.Value, 0);
            using var events = new EventWatch(announcements: true) { AnnouncementSource = window.Value };
            NativeObject Required(string name)
            {
                NativeObject? value = null;
                Wait(() => { atspi_accessible_clear_cache(window.Value); return (value = Find(window.Value, candidate => Name(candidate) == name)) is not null; },
                    "Announcement control: " + name);
                return value!;
            }
            void Invoke(string name)
            {
                using var control = Required(name);
                using var action = new NativeObject(atspi_accessible_get_action_iface(control.Value));
                nint error = 0;
                var accepted = atspi_action_do_action(action.Value, 0, &error);
                Error(ref error);
                if (accepted == 0)
                    throw new InvalidOperationException("The actual announcement action was rejected.");
            }
            void ExpectCount(int count) => Wait(() => { events.Check(); return events.Announcements.Count >= count; },
                "Actual AT-SPI announcement count " + count);
            if (automatic)
            {
                Invoke("Mount message");
                ExpectCount(1);
                Check(events.Announcements[0] == new NativeAnnouncementEvent(1, 0, NativeMessageBarScene.OriginalMessage),
                    "Actual MessageBar mount emits its prepared Korean title/body/actions with polite priority");
                using var message = Required("Native message bar");
                using var title = Required(NativeMessageBarScene.Title);
                Check(Name(message.Value) == "Native message bar" && Name(title.Value) == NativeMessageBarScene.Title,
                    "Actual owned MessageBar group and title remain distinct native children");
                Check(HasNativeLabel(message.Value, title.Value),
                    "Actual AT-SPI labelled-by relation resolves the MessageBar's owned title object");
                Invoke("Make message urgent");
                ExpectCount(2);
                Check(events.Announcements[1] == new NativeAnnouncementEvent(2, 0, NativeMessageBarScene.OriginalMessage),
                    "A MessageBar priority change emits its actual urgent native announcement");
                Invoke("Edit mounted message");
                using var edited = Required("Message quiet : ready : 1");
                Pump();
                events.Check();
                Check(events.Announcements.Count == 2, "Eight frames after a mounted edit do not repeat native MessageBar announcements");
                Invoke("Disable message announcement");
                using var disabled = Required("Message stage : disabled");
                Pump();
                events.Check();
                Check(events.Announcements.Count == 2, "Committed announcement disable emits no replacement native event");
                Invoke("Enable message announcement");
                ExpectCount(3);
                Check(events.Announcements[2] == new NativeAnnouncementEvent(2, 0, NativeMessageBarScene.UpdatedMessage),
                    "Re-enabling MessageBar announces updated actual content once");
                Invoke("Hide message");
                using var hidden = Required("Message stage : hidden");
                Invoke("Mount message");
                ExpectCount(4);
                Check(events.Announcements[3] == events.Announcements[2], "The same owned MessageBar emits once when remounted");
                Invoke("Quiet message frames");
                using var quiet = Required("Message quiet : ready : 2");
                Pump();
                events.Check();
                Check(events.Announcements.Count == 4, "Stable remounted MessageBar frames do not replay native announcements");
            }
            else
            {
                Invoke("Send polite announcement");
                ExpectCount(1);
                Check(events.Announcements[0] == new NativeAnnouncementEvent(1, 0, NativeAnnouncementScene.PoliteMessage),
                    "Actual AT-SPI announcement preserves Korean/emoji text and polite priority");
                Invoke("Send assertive announcement");
                ExpectCount(2);
                Check(events.Announcements[1] == new NativeAnnouncementEvent(2, 0, NativeAnnouncementScene.AssertiveMessage),
                    "Actual AT-SPI urgent announcement preserves assertive priority and Korean text");
                Invoke("Repeat announcement");
                ExpectCount(4);
                Check(events.Announcements[2] == new NativeAnnouncementEvent(1, 0, NativeAnnouncementScene.RepeatMessage)
                    && events.Announcements[3] == events.Announcements[2],
                    "Two explicit equal announcements produce two actual native events");
                Invoke("No announcement frame");
                using var quiet = Required("Quiet frames : ready");
                Pump();
                events.Check();
                Check(events.Announcements.Count == 4, "Eight subsequent native fixture frames do not replay an announcement");
            }
            captured = events.Announcements.ToArray();
            Invoke(automatic ? "Finish message bars" : "Finish announcements");
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new NativeAnnouncementClientResult(
            RuntimeInformation.FrameworkDescription, checks.Count, checks.ToArray(), captured),
            NativeAnnouncementJson.Default.NativeAnnouncementClientResult));
    }
}
