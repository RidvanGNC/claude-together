// Copyright (c) 2026 Rıdvan Gonca. Licensed under the MIT License (see LICENSE). Keep this notice.
// Checks of the agents (Claude, ChatGPT): account homes in AGENT_BASE, what the chats do (hooks), putting the hooks into the settings.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClaudeTogether;

static class AgentTests
{
    static string P(params string[] parts) { return Path.Combine(parts); }

    public static void Run(Action<bool, string> Check)
    {
        Action<string> log = delegate { };
        string src;

        // ---- several agents: ChatGPT (Codex) accounts live in AGENT_BASE\CHATGPT_BASE and share sessions ----
        Config cfgG = new Config();
        Core.AddAccount(cfgG, "one", log);
        Core.AddAccount(cfgG, "two", log);
        Core.AddAccount(cfgG, "gpt1", Providers.ChatGpt, log);
        Core.AddAccount(cfgG, "gpt2", "chatgpt", log);
        Account g1 = cfgG.Accounts.First(a => a.Name == "gpt1"), g2 = cfgG.Accounts.First(a => a.Name == "gpt2");
        Check(g1.Provider == Providers.ChatGpt && !g1.IsClaude && g1.Desktop == Account.NoDesktop && Core.HomeDir(g1) == P(Paths.ChatGptBase, "gpt1"), "chatgpt account: no window, home in AGENT_BASE\\CHATGPT_BASE");
        Config reloaded = Core.Load();
        Account r1 = reloaded.Accounts.First(a => a.Name == "gpt1");
        Check(r1.Provider == Providers.ChatGpt && r1.Dir == g1.Dir && reloaded.Accounts.First(a => a.Name == "one").Provider == Providers.Claude, "provider and home folder survive saving; old entries are Claude");
        try { Core.AddAccount(cfgG, "gpt1", Providers.Claude, log); Check(false, "same name for another agent allowed"); }
        catch (InvalidOperationException) { Check(true, "a name is unique across the agents (the office knows its workers by name)"); }
        try { Core.AddAccount(cfgG, "nobody", "gemini", log); Check(false, "unknown agent allowed"); }
        catch (InvalidOperationException) { Check(true, "an unknown agent is refused"); }
        Check(File.ReadAllText(P(Core.HomeDir(g1), "config.toml")).Contains("cli_auth_credentials_store = \"file\""), "chatgpt account: the sign-in is kept in the home folder, not in the system store");
        Check(Core.IsLink(P(Core.HomeDir(g1), "sessions")) && Core.IsLink(P(Core.HomeDir(g2), "sessions")), "chatgpt accounts link the shared sessions folder");
        File.WriteAllText(P(Core.HomeDir(g1), "sessions", "rollout-1.jsonl"), "chat");
        Check(File.Exists(P(Core.HomeDir(g2), "sessions", "rollout-1.jsonl")) && File.Exists(P(Paths.ChatGptStore, "sessions", "rollout-1.jsonl")), "a chat of one chatgpt account is in the other's sessions");
        Check(!Providers.SignedIn(Providers.ChatGpt, Core.HomeDir(g1)), "chatgpt: not signed in without auth.json");
        File.WriteAllText(P(Core.HomeDir(g1), "auth.json"), "{\"tokens\":{}}");
        Check(Providers.SignedIn(Providers.ChatGpt, Core.HomeDir(g1)) && Core.Ready(g1) && Core.AgentSignedIn(g1) && !Core.Ready(g2), "chatgpt: signed in once auth.json exists");
        string tomlHome = P(Paths.Home, "toml-test");
        Directory.CreateDirectory(tomlHome);
        File.WriteAllText(P(tomlHome, "config.toml"), "model = \"x\"\n[projects.a]\ntrust = true\n");
        Core.EnsureFileCredentials(tomlHome, log);
        string toml = File.ReadAllText(P(tomlHome, "config.toml"));
        Check(toml.StartsWith("cli_auth_credentials_store = \"file\"") && toml.Contains("model = \"x\"") && Directory.GetFiles(tomlHome, "config.toml.backup-*").Length == 1, "an existing config.toml gets the setting on top (before any table) and a copy is kept");
        Core.EnsureFileCredentials(tomlHome, log);
        Check(File.ReadAllText(P(tomlHome, "config.toml")) == toml, "the setting is written once");
        File.WriteAllText(P(tomlHome, "config.toml"), "cli_auth_credentials_store = \"keyring\"\n");
        Core.EnsureFileCredentials(tomlHome, log);
        Check(File.ReadAllText(P(tomlHome, "config.toml")) == "cli_auth_credentials_store = \"keyring\"\n", "a credentials store the user chose is left alone");
        Directory.Delete(tomlHome, true);

        Directory.CreateDirectory(P(Paths.ChatGptBase, "stray"));
        List<ScanEntry> scanG = Core.Scan(cfgG);
        ScanEntry sg = scanG.FirstOrDefault(e => e.Name == "gpt1"), stray = scanG.FirstOrDefault(e => e.Name == "stray");
        Check(sg != null && sg.Registered && sg.Provider == Providers.ChatGpt && sg.CodeSignedIn && !sg.DesktopSignedIn, "scan: a chatgpt account with its agent sign-in");
        Check(stray != null && !stray.Registered && stray.Provider == Providers.ChatGpt && !scanG.Any(e => e.Name == "shared"), "scan: an unregistered folder in CHATGPT_BASE is a hint; the shared folder is not");
        Environment.SetEnvironmentVariable("CODEX_HOME", Core.HomeDir(g2) + Path.DirectorySeparatorChar);
        Account curG = Core.CurrentAccount(cfgG, out src);
        Check(curG != null && curG.Name == "gpt2" && src == "configDir", "whoami: from CODEX_HOME");
        Environment.SetEnvironmentVariable("CODEX_HOME", null);
        try { Core.Delegate(new DelegateRequest { Target = g1, Prompt = "x", ClaudeCommand = "not-started" }); Check(false, "delegate to chatgpt allowed"); }
        catch (InvalidOperationException) { Check(true, "delegate: handing work to a chatgpt account is refused for now"); }
        Check(Core.Snapshot(cfgG)["gpt1"] == false, "a chatgpt account is not 'running' without chats");

        // ---- what the chats are doing: hooks write one small file per chat ----
        HookEvent he = Core.ParseHook("{\"session_id\":\"s1\",\"hook_event_name\":\"PreToolUse\",\"cwd\":\"C:\\\\src\\\\app\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"npm test -- --watch=false\\nsecond line\"}}");
        Check(he != null && he.Name == "PreToolUse" && he.Session == "s1" && he.Tool == "Bash" && he.Detail == "npm test -- --watch=false second line" && he.Cwd == "C:\\src\\app", "hook: event, session, tool and the command are read");
        Check(Core.ParseHook("{ broken") == null && Core.ParseHook("{\"hook_event_name\":\"Stop\"}") == null && Core.ParseHook("[]") == null, "hook: damaged or incomplete calls are ignored");
        Check(Core.ParseHook("{\"session_id\":\"s\",\"hook_event_name\":\"PreToolUse\",\"tool_name\":\"Edit\",\"tool_input\":{\"file_path\":\"C:\\\\src\\\\app\\\\Main.cs\"}}").Detail == "Main.cs", "hook: a file is shown by its name");
        ChatState look = new ChatState();
        look.State = ChatStates.Tool; look.Kind = ToolKinds.Run; look.Tool = "Bash"; look.Detail = "dotnet build";
        Check(Core.ChatStatus(look) == "Running dotnet build", "words: a running command");
        look.Kind = ToolKinds.Edit; look.Detail = "Main.cs";
        Check(Core.ChatStatus(look) == "Editing Main.cs", "words: an edited file");
        look.Kind = ToolKinds.Mcp; look.Tool = "mcp__fs__read";
        Check(Core.ChatStatus(look) == "Using mcp__fs__read", "words: any other tool is named");
        look.State = ChatStates.Waiting; look.Detail = "rm x";
        Check(Core.ChatStatus(look) == "Waiting for you: rm x", "words: waiting for the user shows what for");
        look.State = ChatStates.Thinking; look.Detail = "";
        Check(Core.ChatStatus(look) == "Thinking" && Core.ChatStatus(new ChatState()) == "Idle", "words: thinking and idle");
        Check(Core.AgeText(TimeSpan.FromSeconds(12)) == "12s" && Core.AgeText(TimeSpan.FromSeconds(190)) == "3m" && Core.AgeText(TimeSpan.FromHours(2.5)) == "2h" && Core.AgeText(TimeSpan.FromSeconds(-5)) == "0s", "words: ages are short");
        Check(Core.ToolKind("Bash") == ToolKinds.Run && Core.ToolKind("apply_patch") == ToolKinds.Edit && Core.ToolKind("Grep") == ToolKinds.Read && Core.ToolKind("WebFetch") == ToolKinds.Web && Core.ToolKind("Task") == ToolKinds.Agent && Core.ToolKind("mcp__fs__read") == ToolKinds.Mcp && Core.ToolKind("Whatever") == ToolKinds.Other && Core.ToolKind("") == ToolKinds.None, "hook: tools are told apart by kind");

        DateTime t0 = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        Func<string, string, HookEvent> ev = delegate(string name, string extra)
        {
            return Core.ParseHook("{\"session_id\":\"chat1\",\"hook_event_name\":\"" + name + "\"" + (extra.Length > 0 ? "," + extra : "") + "}");
        };
        ChatState cs = Core.Apply(null, ev("SessionStart", ""), t0);
        Check(cs.State == ChatStates.Idle && cs.Started == t0, "chat: a session starts idle");
        cs = Core.Apply(cs, ev("UserPromptSubmit", "\"prompt\":\"fix the login bug in   the app\\nplease\""), t0.AddSeconds(5));
        Check(cs.State == ChatStates.Thinking && cs.Title == "fix the login bug in the app please", "chat: a prompt starts a turn and names the chat");
        cs = Core.Apply(cs, ev("PreToolUse", "\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"dotnet build\"}"), t0.AddSeconds(8));
        Check(cs.State == ChatStates.Tool && cs.Kind == ToolKinds.Run && cs.Detail == "dotnet build" && cs.Since == t0.AddSeconds(8), "chat: a tool call shows what runs");
        cs = Core.Apply(cs, ev("PermissionRequest", "\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"rm -rf x\"}"), t0.AddSeconds(9));
        Check(cs.State == ChatStates.Waiting && cs.Detail == "rm -rf x", "chat: an approval request means waiting for the user");
        cs = Core.Apply(cs, ev("PostToolUse", "\"tool_name\":\"Bash\""), t0.AddSeconds(20));
        Check(cs.State == ChatStates.Thinking && cs.Tool == "" && cs.Detail == "", "chat: after the tool the chat thinks again");
        cs = Core.Apply(cs, ev("SubagentStart", "\"agent_id\":\"a1\",\"agent_type\":\"Explore\""), t0.AddSeconds(21));
        cs = Core.Apply(cs, ev("SubagentStart", "\"agent_id\":\"a2\",\"agent_type\":\"Plan\""), t0.AddSeconds(22));
        Check(cs.Subs.Count == 2 && cs.Subs[0].Type == "Explore", "chat: sub-agents are counted");
        cs = Core.Apply(cs, ev("SubagentStop", "\"agent_id\":\"a1\""), t0.AddSeconds(30));
        Check(cs.Subs.Count == 1 && cs.Subs[0].Id == "a2", "chat: a sub-agent that stops is removed");
        cs = Core.Apply(cs, ev("Notification", "\"notification_type\":\"permission_prompt\""), t0.AddSeconds(31));
        Check(cs.State == ChatStates.Waiting, "chat: a permission notification means waiting");
        cs = Core.Apply(cs, ev("PreCompact", ""), t0.AddSeconds(32));
        Check(cs.State == ChatStates.Compacting && ChatStates.Busy(cs.State), "chat: compacting counts as busy");
        cs = Core.Apply(cs, ev("Stop", ""), t0.AddSeconds(40));
        Check(cs.State == ChatStates.Idle && cs.Subs.Count == 0 && cs.Title.Length > 0, "chat: stop makes it idle, sub-agents gone, the title stays");
        Check(Core.Apply(cs, ev("SessionEnd", ""), t0.AddSeconds(50)) == null, "chat: the session ending removes it");

        // the files: one per chat, read back, ordered, stale and old ones handled
        if (Directory.Exists(Paths.ActivityDir)) Directory.Delete(Paths.ActivityDir, true);
        Core.RecordHook("one", Providers.Claude, ev("UserPromptSubmit", "\"prompt\":\"first\""), DateTime.UtcNow.AddSeconds(-30));
        HookEvent other = Core.ParseHook("{\"session_id\":\"chat2\",\"hook_event_name\":\"PermissionRequest\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"x\"}}");
        Core.RecordHook("one", Providers.Claude, other, DateTime.UtcNow.AddSeconds(-10));
        List<ChatState> chats = Core.Chats("one", DateTime.UtcNow);
        Check(chats.Count == 2 && chats[0].Session == "chat2" && chats[0].State == ChatStates.Waiting && chats[1].State == ChatStates.Thinking && chats[1].Title == "first", "chats: read back from their files, waiting first");
        Check(Core.ChatPresence(chats) == Presence.Busy && Core.ChatPresence(new List<ChatState>()) == Presence.Away, "chats: busy while one works, away with none");
        Core.RecordHook("one", Providers.Claude, ev("SessionEnd", ""), DateTime.UtcNow);
        Check(Core.Chats("one", DateTime.UtcNow).Count == 1 && Directory.GetFiles(Core.ActivityFolder("one"), "*.tmp").Length == 0, "chats: a ended session's file goes away, no temporary file is left");
        List<ChatState> later = Core.Chats("one", DateTime.UtcNow.AddMinutes(30));
        Check(later.Count == 1 && later[0].Stale && later[0].State == ChatStates.Idle && Core.ChatPresence(later) == Presence.Idle, "chats: busy for 30 minutes without news is shown as idle");
        Check(Core.Chats("one", DateTime.UtcNow.AddHours(8)).Count == 0 && Core.Chats("nobody", DateTime.UtcNow).Count == 0, "chats: old files are forgotten; an unknown account has none");
        File.WriteAllText(P(Core.ActivityFolder("one"), "junk.json"), "{ broken");
        Check(Core.Chats("one", DateTime.UtcNow).Count == 0, "chats: a damaged file does not break reading and is not counted");

        // the account of a hook call: the one the agent runs as
        Environment.SetEnvironmentVariable("CLAUDE_TOGETHER_ACCOUNT", "gpt2");
        Core.RecordHook(Providers.ChatGpt, "{\"session_id\":\"cx1\",\"hook_event_name\":\"UserPromptSubmit\",\"prompt\":\"hello codex\"}");
        Environment.SetEnvironmentVariable("CLAUDE_TOGETHER_ACCOUNT", null);
        List<ChatState> gchats = Core.Chats("gpt2", DateTime.UtcNow);
        Check(gchats.Count == 1 && gchats[0].Provider == Providers.ChatGpt && gchats[0].Title == "hello codex", "hook call: stored under the account the agent runs as");
        Check(Core.Snapshot(cfgG)["gpt2"], "a chatgpt account counts as running while it has a chat");
        Check(Core.Presences(cfgG, Core.Snapshot(cfgG))["gpt2"] == Presence.Busy, "presence: a chatgpt account is busy while its chat works");
        Core.RecordHook(Providers.ChatGpt, "{\"session_id\":\"x\",\"hook_event_name\":\"Stop\"}");   // no account known: ignored
        Check(!Directory.Exists(Core.ActivityFolder("")) || Directory.GetFiles(Core.ActivityFolder("")).Length == 0, "hook call without a known account is dropped");

        // putting the hook into the settings of the agents
        Directory.CreateDirectory(Paths.Store);
        string storeSettings = P(Paths.Store, "settings.json");
        File.WriteAllText(storeSettings, "{\n  \"model\": \"opus\",\n  \"permissions\": { \"allow\": [\"Bash(git status)\"] },\n  \"hooks\": { \"Stop\": [ { \"hooks\": [ { \"type\": \"command\", \"command\": \"notify.cmd\" } ] } ] }\n}");
        Config cfgK = new Config();
        cfgK.Accounts.Add(new Account { Name = "k1", Desktop = Account.SecondaryMode, Dir = P(Paths.ClaudeBase, "k1") });
        cfgK.Accounts.Add(new Account { Name = "gk", Desktop = Account.NoDesktop, Provider = Providers.ChatGpt, Dir = P(Paths.ChatGptBase, "gk") });
        Directory.CreateDirectory(P(Paths.ClaudeBase, "k1")); Directory.CreateDirectory(P(Paths.ChatGptBase, "gk"));
        Check(!Core.HooksInstalled(cfgK), "hooks: not installed at first");
        int changedK = Core.InstallHooks(cfgK, log);
        string settingsText = File.ReadAllText(storeSettings);
        Check(Core.HooksInstalled(cfgK) && changedK >= 2 && Directory.GetFiles(Paths.Store, "settings.json.backup-*").Length == 1, "hooks: installed in the Claude settings and in the Codex hooks.json, the old settings copied aside");
        Check(!File.Exists(P(Paths.ClaudeBase, "k1", "settings.json")), "hooks: a home without settings.json is not given one while a shared settings.json exists (linking it later could replace the shared one)");
        Check(settingsText.Contains("\"model\": \"opus\"") && settingsText.Contains("Bash(git status)") && settingsText.Contains("notify.cmd"), "hooks: the user's own settings and hooks are kept");
        Check(settingsText.Contains("\"args\": [\"hook\", \"claude\"]") && settingsText.Contains("\"async\": true") && settingsText.Contains("PostToolUse") && settingsText.Contains("SessionEnd"), "hooks: Claude starts our program directly, in the background, for each step");
        string codexHooks = File.ReadAllText(P(Paths.ChatGptBase, "gk", "hooks.json"));
        Check(codexHooks.Contains("hook chatgpt") && codexHooks.Contains("commandWindows") && codexHooks.Contains("\"Interrupt\""), "hooks: Codex gets a hooks.json in the account's home");
        string firstText = settingsText;
        Check(Core.InstallHooks(cfgK, log) == 0 && File.ReadAllText(storeSettings) == firstText, "hooks: installing again changes nothing");
        int removedK = Core.RemoveHooks(cfgK, log);
        string afterRemove = File.ReadAllText(storeSettings);
        Check(!Core.HooksInstalled(cfgK) && removedK >= 2 && !afterRemove.Contains("claude-together") && afterRemove.Contains("notify.cmd") && afterRemove.Contains("\"model\": \"opus\"") && !afterRemove.Contains("PostToolUse"), "hooks: removing takes only our entries out");
        Check(!File.Exists(P(Paths.ChatGptBase, "gk", "hooks.json")) && Directory.GetFiles(P(Paths.ChatGptBase, "gk"), "hooks.json.removed-*").Length == 1, "hooks: a hooks.json that only held ours is moved aside, not deleted");
        File.WriteAllText(storeSettings, "{ not json");
        Check(Core.InstallHooks(cfgK, log) >= 0 && File.ReadAllText(storeSettings) == "{ not json", "hooks: a settings file that is not JSON is left alone");
        File.Delete(storeSettings);
        Core.InstallHooks(cfgK, log);
        Check(File.Exists(P(Paths.ClaudeBase, "k1", "settings.json")) && Core.HooksInstalled(cfgK), "hooks: without a shared settings.json the home gets its own");
        Core.RemoveHooks(cfgK, log);
        File.WriteAllText(storeSettings, "{}");
        Check(Core.CodexHookCommand("C:\\Tools\\claude-together.exe", "chatgpt") == "C:\\Tools\\claude-together.exe hook chatgpt", "hooks: a path without spaces is used as it is for Codex");


        // ---- the optional online features: place, weather, prayer times (parsing only; the requests are made by Online) ----
        Place pl = Online.ParsePlace("{\"results\":[{\"name\":\"Istanbul\",\"latitude\":41.01384,\"longitude\":28.94966,\"country\":\"Türkiye\"}]}");
        Check(pl != null && pl.Name == "Istanbul, Türkiye" && pl.Lat == 41.01 && pl.Lon == 28.95, "place: the name and the coordinates (rounded to two decimals) are read");
        Check(Online.ParsePlace("{}") == null && Online.ParsePlace("{\"results\":[]}") == null && Online.ParsePlace("{ broken") == null && Online.ParsePlace("[]") == null, "place: nothing found, damaged answers and the wrong shape give no place");
        WeatherNow wn = Online.ParseWeather("{\"current\":{\"time\":\"2026-11-24T12:00\",\"temperature_2m\":12.34,\"weather_code\":61,\"is_day\":1}}");
        Check(wn != null && wn.TempC == 12.3 && wn.Code == 61 && wn.Sky == Sky.Rain && wn.IsDay, "weather: temperature, code and the sky are read");
        Check(Online.ParseWeather("{\"current\":{}}") == null && Online.ParseWeather("nonsense") == null, "weather: an answer without the numbers gives nothing");
        Check(Online.SkyOf(0) == Sky.Clear && Online.SkyOf(2) == Sky.Cloudy && Online.SkyOf(45) == Sky.Fog && Online.SkyOf(53) == Sky.Rain && Online.SkyOf(81) == Sky.Rain && Online.SkyOf(73) == Sky.Snow && Online.SkyOf(86) == Sky.Snow && Online.SkyOf(96) == Sky.Storm, "weather: the codes become clear, cloudy, fog, rain, snow and storm");
        DateTime day = new DateTime(2026, 11, 24);
        PrayerDay pd = Online.ParsePrayerDay("{\"data\":{\"timings\":{\"Fajr\":\"05:12 (+03)\",\"Sunrise\":\"06:40\",\"Dhuhr\":\"13:05\",\"Asr\":\"16:20\",\"Maghrib\":\"19:30\",\"Isha\":\"20:55\"}}}", day);
        Check(pd != null && pd.Date == day && pd.Times[0] == day.AddHours(5).AddMinutes(12) && pd.Times[1] == day.AddHours(13).AddMinutes(5) && pd.Times[4] == day.AddHours(20).AddMinutes(55), "prayer times: the five prayers of the day are read (a time zone note after the time is ignored)");
        Check(Online.ParsePrayerDay("{\"data\":{\"timings\":{\"Fajr\":\"05:12\"}}}", day) == null && Online.ParsePrayerDay("{\"data\":{\"timings\":{\"Fajr\":\"09:00\",\"Dhuhr\":\"08:00\",\"Asr\":\"16:00\",\"Maghrib\":\"19:00\",\"Isha\":\"21:00\"}}}", day) == null && Online.ParsePrayerDay("junk", day) == null, "prayer times: a day that is incomplete or not in order is refused");
        Check(Online.PrayerDue(pd, day.AddHours(13).AddMinutes(5).AddSeconds(30), -1) == 1 && Online.PrayerDue(pd, day.AddHours(13).AddMinutes(4), -1) == -1 && Online.PrayerDue(pd, day.AddHours(13).AddMinutes(8), -1) == -1, "prayer times: the call comes when the time has come, not before and not minutes late");
        Check(Online.PrayerDue(pd, day.AddHours(13).AddMinutes(5).AddSeconds(30), 1) == -1 && Online.PrayerDue(pd, day.AddHours(13).AddMinutes(5).AddSeconds(30), 0) == 1 && Online.PrayerDue(pd, day.AddDays(1).AddHours(13).AddMinutes(5), -1) == -1 && Online.PrayerDue(null, day, -1) == -1, "prayer times: each is called once; another day or no times give no call");

        // the settings: off by default, kept with the place
        string settingsBackup = File.Exists(Paths.SettingsFile) ? File.ReadAllText(Paths.SettingsFile) : null;
        AppSettings fresh = new AppSettings();
        Check(!fresh.Weather && !fresh.PrayerTimes && !fresh.HasPlace, "online: the weather and the prayer times are off until the user switches them on");
        AppSettings chosen = new AppSettings();
        chosen.Weather = true; chosen.PrayerTimes = true; chosen.HasPlace = true; chosen.PlaceName = "Ankara, Türkiye"; chosen.Latitude = 39.92077; chosen.Longitude = 32.85411;
        Updates.SaveSettings(chosen);
        AppSettings back = Updates.LoadSettings();
        Check(back.Weather && back.PrayerTimes && back.HasPlace && back.PlaceName == "Ankara, Türkiye" && back.Latitude == 39.92 && back.Longitude == 32.85, "online: the choices and the place (rounded to two decimals) are kept");
        if (settingsBackup != null) File.WriteAllText(Paths.SettingsFile, settingsBackup); else File.Delete(Paths.SettingsFile);

        // a feed folder stands in for the internet; with the switch off nothing is asked at all
        string feed = P(Paths.Home, "online-feed");
        Directory.CreateDirectory(feed);
        File.WriteAllText(P(feed, "place.json"), "{\"results\":[{\"name\":\"Izmir\",\"latitude\":38.4237,\"longitude\":27.1428,\"country\":\"Türkiye\"}]}");
        File.WriteAllText(P(feed, "weather.json"), "{\"current\":{\"temperature_2m\":3.0,\"weather_code\":73,\"is_day\":0}}");
        File.WriteAllText(P(feed, "prayer.json"), "{\"data\":{\"timings\":{\"Fajr\":\"05:00\",\"Dhuhr\":\"13:00\",\"Asr\":\"16:00\",\"Maghrib\":\"19:00\",\"Isha\":\"20:30\"}}}");
        Environment.SetEnvironmentVariable(Online.FeedVariable, feed);
        Place izmir = Online.FindPlace("izmir");
        Check(izmir != null && izmir.Name.StartsWith("Izmir") && Online.FetchWeather(izmir).Sky == Sky.Snow && !Online.FetchWeather(izmir).IsDay && Online.FetchPrayerDay(izmir, day).Times[2] == day.AddHours(16), "online: with a feed folder the three answers are read the same way");
        Environment.SetEnvironmentVariable(Online.FeedVariable, null);
        Environment.SetEnvironmentVariable(Online.OffVariable, "1");
        Check(!Online.Allowed && Online.FindPlace("izmir") == null && Online.FetchWeather(izmir) == null && Online.FetchPrayerDay(izmir, day) == null, "online: with CLAUDE_TOGETHER_NO_ONLINE=1 no request is made and nothing is answered");
        Environment.SetEnvironmentVariable(Online.OffVariable, null);
    }
}
