using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless balance harness: plays whole matches with every seat, including the player's, flown by the AI, and writes
// per-match telemetry so balance is argued from numbers rather than from reading the constants.
//
//   "<Unity>" -batchmode -nographics -projectPath "<project>" -executeMethod RiftBalanceRunner.Run -logFile <path>
//
// Env: RIFT_BALANCE_MATCHES (default 4), RIFT_BALANCE_OUT (default <repo>/docs/balance/latest.json).
// The world is stepped by hand at a fixed 0.02 s through the same dt methods the verification suites use, 250 steps
// per editor frame, so a 300 s match takes about sixty editor frames. The live player loop still ticks alongside but
// Time.maximumDeltaTime is pinned to one fixed step so its contribution is under half a percent of match time.
//
// Exit codes: 0 = all matches finished and the report was written. 1 = an exception during play (the log has it).
// 2 = never entered Play Mode.
public static class RiftBalanceRunner
{
    const float Dt = .02f;
    const int StepsPerFrame = 250;
    const double TimeoutSeconds = 600;
    static double deadline;
    static int matches, matchIndex, framesInPlay;
    static string outPath;
    static readonly List<MatchStats> results = new List<MatchStats>();
    static MatchStats current;

    class PilotStats
    {
        public string callsign, tag; public int id;
        public int kills, deaths, crashes, burns, shotDown, banks, claims, cargoLost, bankedTotal, shots, hits, combatShots;
        public float firstKillAt = -1, aliveTime, timeCarrying;
        public int peakCargo, cargoAtDeathSum;
        // Engagement telemetry: how long the pilot had a rival target and how close it got, plus a 6 s altitude/plunge
        // trace so every death can be read back as a trajectory instead of a guess.
        public float timeWithTarget, timeInRange, timeRecovering;
        public Dictionary<string, float> tacticTime = new Dictionary<string, float>();
        public float timeAbove650, altitudeSum; public int altitudeSamples; public bool launched, bankSampled;
        public Queue<string> trace = new Queue<string>();
        public float traceTimer;
        public List<string> deathTraces = new List<string>();
    }
    class MatchStats
    {
        public int index, standInPersona; public float length;
        public PilotStats[] pilots;
        public int overcharges, overchargeBanks, contests, totalKills, totalCrashes, totalBanks, totalClaims;
        public List<int> killsPerMinute = new List<int>();
        public float firstKillAt = -1;
        public int[] prevDeaths, prevKills, prevScore, prevCargo, prevOwner;
        public bool prevSurge;
        public float clock;
    }

    [MenuItem("Rift/Run headless balance matches (CI)")]
    public static void Run()
    {
        matches = 4; int.TryParse(Environment.GetEnvironmentVariable("RIFT_BALANCE_MATCHES"), out int m); if (m > 0) matches = m;
        outPath = Environment.GetEnvironmentVariable("RIFT_BALANCE_OUT");
        if (string.IsNullOrEmpty(outPath)) outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/balance/latest.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.playModeStateChanged += OnStateChanged;
        EditorApplication.update += WatchForTimeout;
        EditorApplication.isPlaying = true;
    }

    static void WatchForTimeout()
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= WatchForTimeout; EditorApplication.update -= Step;
        Debug.LogError("[BAL] TIMEOUT"); EditorApplication.Exit(2);
    }

    static void OnStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnStateChanged;
        framesInPlay = 0; matchIndex = -1;
        Time.maximumDeltaTime = Dt;
        EditorApplication.update += Step;
    }

    static void Step()
    {
        if (++framesInPlay < 20) return; // let Boot() and the first Spawn settle
        var g = AerialCombatPrototype.I;
        try
        {
            if (g == null) throw new Exception("arena missing");
            if (current == null) BeginMatch(g);
            for (int i = 0; i < StepsPerFrame && current != null; i++) Simulate(g);
            if (current == null && matchIndex + 1 >= matches) Finish();
        }
        catch (Exception e)
        {
            EditorApplication.update -= Step; EditorApplication.update -= WatchForTimeout;
            Debug.LogError("[BAL] FAILED: " + e.Message + "\n" + e.StackTrace);
            EditorApplication.isPlaying = false; EditorApplication.Exit(1);
        }
    }

    static void BeginMatch(AerialCombatPrototype g)
    {
        matchIndex++;
        g.paused = false; g.difficulty = 1; g.RestartMatch();
        // The stand-in for the player rotates through the seven personalities so no single style biases the field.
        int persona = 1 + matchIndex % 7; g.player.autopilot = persona;
        current = new MatchStats { index = matchIndex, standInPersona = persona, pilots = new PilotStats[g.pilots.Count] };
        int n = g.pilots.Count;
        current.prevDeaths = new int[n]; current.prevKills = new int[n]; current.prevScore = new int[n]; current.prevCargo = new int[n];
        current.prevOwner = new int[g.gates.Count];
        for (int i = 0; i < n; i++)
        {
            var p = g.pilots[i];
            current.pilots[i] = new PilotStats { callsign = p.callsign, tag = ArenaPersonality.For(p == g.player ? persona : p.id).tag, id = p.id };
            current.prevKills[i] = p.kills; current.prevDeaths[i] = p.deaths; current.prevScore[i] = p.score; current.prevCargo[i] = p.cargo;
            p.shotsFired = 0; p.combatShotsFired = 0; p.hitsLanded = 0;
        }
        for (int i = 0; i < g.gates.Count; i++) current.prevOwner[i] = g.gates[i].owner;
        Debug.Log("[BAL] match " + matchIndex + " begins, stand-in persona " + ArenaPersonality.For(persona).tag);
    }

    static void Simulate(AerialCombatPrototype g)
    {
        var c = current;
        foreach (var p in g.pilots) p.Simulate(Dt);
        foreach (var b in g.bolts.ToArray()) if (b) b.Tick(Dt);
        foreach (var gate in g.gates) gate.Tick(Dt);
        foreach (var core in g.cores) core.Tick(Dt);
        foreach (var s in g.shards.ToArray()) if (s) s.Tick(Dt);
        g.MatchTick(Dt);
        c.clock += Dt;
        if (g.phase != MatchPhase.Playing && g.phase != MatchPhase.Ended) return;
        // Telemetry: diff every counter against the previous step and attribute each death to a killer or the planet.
        int killerThisStep = -1;
        for (int i = 0; i < g.pilots.Count; i++) if (g.pilots[i].kills > c.prevKills[i]) killerThisStep = i;
        for (int i = 0; i < g.pilots.Count; i++)
        {
            var p = g.pilots[i]; var s = c.pilots[i];
            if (p.Alive)
            {
                s.aliveTime += Dt; if (p.cargo > 0) s.timeCarrying += Dt; s.peakCargo = Math.Max(s.peakCargo, p.cargo);
                var foe = p.CombatTarget;
                if (foe) { s.timeWithTarget += Dt; if (Vector3.Distance(p.transform.position, foe.transform.position) < 650) s.timeInRange += Dt; }
                if (p.recovering) s.timeRecovering += Dt;
                string tac = p.tactic ?? "?"; s.tacticTime.TryGetValue(tac, out float acc); s.tacticTime[tac] = acc + Dt;
                if (p.Altitude > 650) s.timeAbove650 += Dt; s.altitudeSum += p.Altitude; s.altitudeSamples++;
                // A launch is the moment a pilot is committed to leaving the fields: log how it got there, once per life.
                float rise = Vector3.Dot(p.velocity, AerialCombatPrototype.Up(p.transform.position));
                if (!s.launched && p.Altitude > 700 && rise > 40) { s.launched = true; Debug.Log("[BAL] launch m" + c.index + " " + p.callsign + " :: " + string.Join(" | ", s.trace)); }
                if (p.Altitude < 400) s.launched = false;
                s.traceTimer += Dt;
                if (s.traceTimer >= .5f)
                {
                    s.traceTimer = 0;
                    Vector3 up = AerialCombatPrototype.Up(p.transform.position);
                    s.trace.Enqueue("t=" + c.clock.ToString("F0") + " alt=" + p.Altitude.ToString("F0") + " spd=" + p.Speed.ToString("F0") + " plunge=" + (-Vector3.Dot(p.velocity, up)).ToString("F0") + " heat=" + p.hullHeat.ToString("F2") + " thr=" + p.throttle.ToString("F2") + " " + p.tactic + (p.recovering ? " REC" : "") + " cargo=" + p.cargo + " hp=" + p.health.ToString("F0") + " nav=" + Vector3.Distance(p.Navigation, p.transform.position).ToString("F0"));
                    while (s.trace.Count > 12) s.trace.Dequeue();
                    // Banking approach sampler: every 10 s, how far a cargo-laden pilot heading for a ring still is from it.
                    if (p.GateTarget && p.cargo > 0 && Mathf.FloorToInt(c.clock) % 10 == 0 && s.traceTimer == 0 && !s.bankSampled)
                    {
                        s.bankSampled = true;
                        Vector3 toGate = p.GateTarget.transform.position - p.transform.position;
                        Debug.Log("[BAL] bank m" + c.index + " " + p.callsign + " t=" + c.clock.ToString("F0") + " cargo=" + p.cargo + " dist=" + toGate.magnitude.ToString("F0") + " angle=" + Vector3.Angle(p.transform.forward, toGate).ToString("F0") + " nav=" + Vector3.Distance(p.Navigation, p.transform.position).ToString("F0") + " runIn=" + (Vector3.Distance(p.Navigation, p.GateTarget.transform.position) > 1) + " alt=" + p.Altitude.ToString("F0") + " gateAlt=" + AerialCombatPrototype.Altitude(p.GateTarget.transform.position).ToString("F0") + " climb=" + Vector3.Dot(p.velocity, up).ToString("F0") + " spd=" + p.Speed.ToString("F0") + " thr=" + p.throttle.ToString("F2") + " ctrl=" + p.controls.ToString("F1") + " " + p.tactic);
                    }
                    if (Mathf.FloorToInt(c.clock) % 10 != 0) s.bankSampled = false;
                }
            }
            if (p.deaths > c.prevDeaths[i])
            {
                s.deaths++; s.cargoAtDeathSum += c.prevCargo[i]; s.cargoLost += c.prevCargo[i];
                // The body is still where it died: under 6 m it hit the ground whatever its skin temperature was.
                string cause = killerThisStep >= 0 && killerThisStep != i ? "shot by " + g.pilots[killerThisStep].callsign : p.Altitude < 6 ? "TERRAIN" : p.hullHeat > 1 ? "BURN" : "CEILING";
                string trail = string.Join(" | ", s.trace);
                s.deathTraces.Add(cause + " :: " + trail);
                Debug.Log("[BAL] death m" + c.index + " " + p.callsign + " " + cause + " :: " + trail);
                s.trace.Clear();
                if (killerThisStep >= 0 && killerThisStep != i) s.shotDown++;
                else if (p.Altitude < 6) s.crashes++;
                else s.burns++;
                c.totalKills += killerThisStep >= 0 && killerThisStep != i ? 1 : 0;
                c.totalCrashes += killerThisStep >= 0 && killerThisStep != i ? 0 : 1;
            }
            if (p.kills > c.prevKills[i])
            {
                s.kills += p.kills - c.prevKills[i];
                if (s.firstKillAt < 0) s.firstKillAt = c.clock;
                if (c.firstKillAt < 0) c.firstKillAt = c.clock;
                int minute = Mathf.FloorToInt(c.clock / 60); while (c.killsPerMinute.Count <= minute) c.killsPerMinute.Add(0); c.killsPerMinute[minute]++;
            }
            if (p.score > c.prevScore[i])
            {
                int gained = p.score - c.prevScore[i]; s.bankedTotal += gained;
                // A claim is exactly +25 on an owner change; anything else on the same step is a cargo deposit or income.
                bool claimed = false;
                for (int k = 0; k < g.gates.Count; k++) if (g.gates[k].owner == p.id && c.prevOwner[k] != p.id) claimed = true;
                if (claimed) { s.claims++; c.totalClaims++; }
                if (gained != 25 && gained != 5) { s.banks++; c.totalBanks++; if (g.OverchargedGate()) c.overchargeBanks++; }
            }
            c.prevKills[i] = p.kills; c.prevDeaths[i] = p.deaths; c.prevScore[i] = p.score; c.prevCargo[i] = p.cargo;
        }
        for (int k = 0; k < g.gates.Count; k++) c.prevOwner[k] = g.gates[k].owner;
        bool surge = g.OverchargedGate() != null;
        if (surge && !c.prevSurge) c.overcharges++;
        c.prevSurge = surge;
        if (g.phase == MatchPhase.Ended) EndMatch(g);
    }

    static void EndMatch(AerialCombatPrototype g)
    {
        var c = current; c.length = c.clock;
        for (int i = 0; i < g.pilots.Count; i++)
        {
            var p = g.pilots[i]; var s = c.pilots[i];
            s.shots = p.shotsFired; s.combatShots = p.combatShotsFired; s.hits = p.hitsLanded;
        }
        results.Add(c);
        var sb = new StringBuilder("[BAL] match " + c.index + " done in " + c.length.ToString("F0") + " s: kills=" + c.totalKills + " crashes=" + c.totalCrashes + " banks=" + c.totalBanks + " claims=" + c.totalClaims + " overcharges=" + c.overcharges + " firstKill=" + c.firstKillAt.ToString("F0") + "s |");
        foreach (var s in c.pilots) sb.Append(" " + s.callsign + "(" + s.tag + ") " + s.bankedTotal + "b/" + s.kills + "k/" + s.deaths + "d");
        Debug.Log(sb.ToString());
        foreach (var d in UnityEngine.Object.FindObjectsByType<ArenaDebris>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(d.gameObject);
        current = null;
    }

    static List<string> TacticEntries(PilotStats s)
    {
        var list = new List<string>();
        foreach (var kv in s.tacticTime) list.Add("\"" + kv.Key.Replace("\"", "'") + "\": " + kv.Value.ToString("F0"));
        return list;
    }

    static void Finish()
    {
        EditorApplication.update -= Step; EditorApplication.update -= WatchForTimeout;
        var sb = new StringBuilder();
        sb.Append("{\n \"generated\": \"").Append(DateTime.UtcNow.ToString("o")).Append("\",\n \"dt\": ").Append(Dt.ToString("F3")).Append(",\n \"matches\": [\n");
        for (int m = 0; m < results.Count; m++)
        {
            var c = results[m];
            sb.Append("  {\"index\": ").Append(c.index).Append(", \"standIn\": \"").Append(ArenaPersonality.For(c.standInPersona).tag).Append("\", \"length\": ").Append(c.length.ToString("F1"))
              .Append(", \"kills\": ").Append(c.totalKills).Append(", \"crashes\": ").Append(c.totalCrashes).Append(", \"banks\": ").Append(c.totalBanks).Append(", \"claims\": ").Append(c.totalClaims)
              .Append(", \"overcharges\": ").Append(c.overcharges).Append(", \"overchargeBanks\": ").Append(c.overchargeBanks).Append(", \"firstKillAt\": ").Append(c.firstKillAt.ToString("F1"))
              .Append(", \"killsPerMinute\": [").Append(string.Join(",", c.killsPerMinute)).Append("],\n   \"pilots\": [\n");
            for (int i = 0; i < c.pilots.Length; i++)
            {
                var s = c.pilots[i];
                sb.Append("    {\"callsign\": \"").Append(s.callsign).Append("\", \"tag\": \"").Append(s.tag).Append("\", \"banked\": ").Append(s.bankedTotal)
                  .Append(", \"kills\": ").Append(s.kills).Append(", \"deaths\": ").Append(s.deaths).Append(", \"shotDown\": ").Append(s.shotDown).Append(", \"crashes\": ").Append(s.crashes).Append(", \"burns\": ").Append(s.burns)
                  .Append(", \"banks\": ").Append(s.banks).Append(", \"claims\": ").Append(s.claims).Append(", \"cargoLost\": ").Append(s.cargoLost).Append(", \"peakCargo\": ").Append(s.peakCargo)
                  .Append(", \"aliveTime\": ").Append(s.aliveTime.ToString("F0")).Append(", \"timeCarrying\": ").Append(s.timeCarrying.ToString("F0"))
                  .Append(", \"shots\": ").Append(s.shots).Append(", \"combatShots\": ").Append(s.combatShots).Append(", \"hits\": ").Append(s.hits).Append(", \"firstKillAt\": ").Append(s.firstKillAt.ToString("F1"))
                  .Append(", \"timeWithTarget\": ").Append(s.timeWithTarget.ToString("F0")).Append(", \"timeInRange\": ").Append(s.timeInRange.ToString("F0")).Append(", \"timeRecovering\": ").Append(s.timeRecovering.ToString("F0"))
                  .Append(", \"timeAbove650\": ").Append(s.timeAbove650.ToString("F0")).Append(", \"meanAltitude\": ").Append((s.altitudeSamples > 0 ? s.altitudeSum / s.altitudeSamples : 0).ToString("F0"))
                  .Append(", \"tactics\": {").Append(string.Join(",", TacticEntries(s))).Append("}")
                  .Append(", \"deaths_detail\": [").Append(string.Join(",", s.deathTraces.ConvertAll(t => "\"" + t.Replace("\"", "'") + "\""))).Append("]}")
                  .Append(i < c.pilots.Length - 1 ? ",\n" : "\n");
            }
            sb.Append("   ]}").Append(m < results.Count - 1 ? ",\n" : "\n");
        }
        sb.Append(" ]\n}\n");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("[BAL] PASS: " + results.Count + " matches written to " + outPath);
        EditorApplication.isPlaying = false; EditorApplication.Exit(0);
    }
}
