namespace AgentCouncil.Web.Components.Pages;

/// <summary>
/// The village's drawings as inline SVG: houses, people, the throne and trees. All share the --v-ink outline.
/// People face right; CSS mirrors them, and swings the .leg-*/.arm-* groups to walk.
/// </summary>
internal static class VillageArt
{
    private const string Ink = "var(--v-ink)";

    // Left to right along the hill. X is the door's centre in % of the scene; every house's door sits at x=80 of its 160-wide drawing.
    internal static readonly (string Name, double X, string Svg)[] Houses =
    [
        ("Optimistic", 12, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <rect x="30" y="64" width="100" height="76" rx="8" fill="#F7E3A1" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M16 72 Q80 4 144 72 Z" fill="var(--optimistic)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M42 58 Q80 24 118 58" fill="none" stroke="var(--v-ink)" stroke-width="2" opacity=".3"/>
              <circle cx="52" cy="96" r="10" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <circle cx="108" cy="96" r="10" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M66 140 V114 a14 14 0 0 1 28 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M66 140 V114 a14 14 0 0 1 28 0 V140 Z" fill="#E88B3A" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M16 140 V118 M146 140 V122" stroke="var(--v-ink)" stroke-width="2"/>
              <circle cx="16" cy="114" r="6" fill="#F29BB5" stroke="var(--v-ink)" stroke-width="2"/>
              <circle cx="146" cy="118" r="6" fill="#F29BB5" stroke="var(--v-ink)" stroke-width="2"/>
            </svg>
            """),
        ("Skeptic", 31, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <path d="M60 140 L63 46 L97 42 L100 140 Z" fill="#E1D9C9" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M52 50 L82 4 L108 44 Z" fill="var(--skeptic)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <g class="scope"><path d="M96 60 L132 42 L136 50 L100 68 Z" fill="#5E6E76" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <circle cx="134" cy="46" r="5" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="2"/></g>
              <rect x="73" y="60" width="14" height="18" rx="7" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <circle cx="80" cy="98" r="7" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M69 140 V122 a11 11 0 0 1 22 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M69 140 V122 a11 11 0 0 1 22 0 V140 Z" fill="#8E5A3C" stroke="var(--v-ink)" stroke-width="3"/>
            </svg>
            """),
        ("Moderator", 50, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <rect x="18" y="68" width="124" height="72" fill="#ECE8DD" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M80 24 V2" stroke="var(--v-ink)" stroke-width="3"/>
              <path class="flag" d="M81 3 L102 8 L81 14 Z" fill="var(--moderator)" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
              <path d="M8 70 L80 24 L152 70 Z" fill="var(--moderator)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <circle cx="80" cy="52" r="10" fill="#FFFDF6" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M80 52 V45 M80 52 L85 55" stroke="var(--v-ink)" stroke-width="2" stroke-linecap="round"/>
              <g fill="#D8D2C3" stroke="var(--v-ink)" stroke-width="2.5">
                <rect x="30" y="78" width="9" height="62"/><rect x="50" y="78" width="9" height="62"/>
                <rect x="101" y="78" width="9" height="62"/><rect x="121" y="78" width="9" height="62"/>
              </g>
              <path d="M66 140 V116 a14 14 0 0 1 28 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M66 140 V116 a14 14 0 0 1 28 0 V140 Z" fill="#3F5BB8" stroke="var(--v-ink)" stroke-width="3"/>
            </svg>
            """),
        ("Pragmatic", 69, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <g class="smoke" fill="#DADFE2"><circle cx="118" cy="20" r="6"/><circle cx="118" cy="20" r="6"/><circle cx="118" cy="20" r="6"/></g>
              <rect x="110" y="26" width="16" height="40" fill="#8A6B55" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="12" y="76" width="136" height="64" fill="#D6B48E" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M4 80 L80 38 L156 80 Z" fill="var(--pragmatic)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <rect x="22" y="92" width="22" height="16" fill="#F2925B" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="56" y="96" width="48" height="44" fill="#3A2E28"/>
              <g class="door"><rect x="56" y="96" width="48" height="44" fill="#8A5A36" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M56 96 L104 140 M104 96 L56 140" stroke="var(--v-ink)" stroke-width="2" opacity=".45"/></g>
              <path d="M126 126 h24 l-5 5 h-4 v9 h-8 v-9 h-4 z" fill="#4A5258" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
            </svg>
            """),
        ("Pessimistic", 88, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <path d="M46 140 L50 80 L114 76 L112 140 Z" fill="#C3BDD1" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M36 86 Q58 74 80 56 Q100 70 126 80 Z" fill="var(--pessimistic)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <rect x="94" y="96" width="12" height="12" transform="rotate(9 100 102)" fill="#AEB7C2" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M70 140 V118 H90 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M70 140 V118 H90 V140 Z" fill="#6B6380" stroke="var(--v-ink)" stroke-width="3"/>
              <g class="rain" stroke="#6F8FB0" stroke-width="2.5" stroke-linecap="round">
                <path d="M62 52 l-2 6"/><path d="M74 52 l-2 6"/><path d="M86 52 l-2 6"/><path d="M98 52 l-2 6"/>
              </g>
              <path d="M52 34 a11 11 0 0 1 20 -8 a14 14 0 0 1 26 3 a10 10 0 0 1 2 19 H56 a9 9 0 0 1 -4 -14 Z" fill="#9AA1AD" stroke="var(--v-ink)" stroke-width="2.5" stroke-linejoin="round"/>
              <ellipse cx="134" cy="138" rx="14" ry="3" fill="#8FA6BF" opacity=".7"/>
            </svg>
            """),
    ];

    /// <summary>Each debater as a person: outfit and the thing in their hand say who they are.</summary>
    internal static readonly Dictionary<string, string> People = new()
    {
        // The elder: white hair and beard, long robe, a staff with the council's blue orb.
        ["Moderator"] = Person("#E8B48A", "var(--moderator)", "#3F4A6B", robe: true,
            hair: """
                <path d="M19 24 Q19 12 30 12 Q41 12 41 24 Q38 17 30 17 Q22 17 19 24 Z" fill="#EDEDED" stroke="var(--v-ink)" stroke-width="2"/>
                <path d="M21 28 Q30 46 40 28 Q36 34 30 34 Q24 34 21 28 Z" fill="#EDEDED" stroke="var(--v-ink)" stroke-width="1.5"/>
                """,
            hand: """
                <path d="M44.5 26 V98" stroke="#7A5634" stroke-width="3" stroke-linecap="round"/>
                <circle cx="44.5" cy="23" r="4.5" fill="var(--moderator)" stroke="var(--v-ink)" stroke-width="2"/>
                """),

        // Straw hat and a flower picked on the way.
        ["Optimistic"] = Person("#F1C27D", "var(--optimistic)", "#5B4B3A",
            hair: """
                <path d="M19 24 Q20 15 30 15 Q40 15 41 24 Q34 19 26 20 Z" fill="#D9772B" stroke="var(--v-ink)" stroke-width="1.5"/>
                <ellipse cx="30" cy="16" rx="17" ry="4" fill="#E9C46A" stroke="var(--v-ink)" stroke-width="2"/>
                <path d="M22 16 Q23 5 30 5 Q37 5 38 16 Z" fill="#E9C46A" stroke="var(--v-ink)" stroke-width="2"/>
                <path d="M22.6 13 H37.4" stroke="var(--optimistic)" stroke-width="2.5"/>
                """,
            hand: """
                <path d="M44.5 68 V48" stroke="#4E7F3E" stroke-width="2"/>
                <circle cx="44.5" cy="45" r="5.5" fill="#F4A23B" stroke="var(--v-ink)" stroke-width="1.5"/>
                <circle cx="44.5" cy="45" r="2" fill="#7A4A1E"/>
                """),

        // Spectacles and a magnifying glass, for the receipts.
        ["Skeptic"] = Person("#C68642", "var(--skeptic)", "#3A3F4A",
            hair: """
                <path d="M19 24 Q18 12 30 12 Q42 12 41 22 Q36 16 28 17 Q22 18 19 24 Z" fill="#2B2B2B" stroke="var(--v-ink)" stroke-width="1.5"/>
                <circle cx="29" cy="25" r="3.6" fill="#D4EAF3" fill-opacity=".45" stroke="var(--v-ink)" stroke-width="1.5"/>
                <circle cx="35.5" cy="25" r="3.6" fill="#D4EAF3" fill-opacity=".45" stroke="var(--v-ink)" stroke-width="1.5"/>
                """,
            hand: """
                <path d="M44.5 67 L49 56" stroke="#5E4630" stroke-width="3" stroke-linecap="round"/>
                <circle cx="51" cy="51" r="6" fill="#D4EAF3" fill-opacity=".7" stroke="var(--v-ink)" stroke-width="2"/>
                """),

        // The smith: leather apron and a hammer.
        ["Pragmatic"] = Person("#8D5524", "#7A6250", "#4A3B30",
            hair: """<path d="M19 22 Q20 13 30 13 Q40 13 41 22 Q35 17 30 17 Q25 17 19 22 Z" fill="#2E2018" stroke="var(--v-ink)" stroke-width="1.5"/>""",
            front: """<path d="M21 48 H39 L41 74 H19 Z" fill="var(--pragmatic)" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>""",
            hand: """
                <path d="M44.5 70 V50" stroke="#7A5634" stroke-width="3" stroke-linecap="round"/>
                <rect x="38" y="44" width="13" height="7" rx="1.5" fill="#5A6268" stroke="var(--v-ink)" stroke-width="1.5"/>
                """),

        // Hooded, frowning, never without an umbrella.
        ["Pessimistic"] = Person("#E0AC69", "var(--pessimistic)", "#4B4560", robe: true, frown: true,
            hair: """<path d="M17 31 Q15 9 30 9 Q45 9 43 31 Q42 18 30 17 Q18 18 17 31 Z" fill="var(--pessimistic)" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>""",
            hand: """
                <path d="M44.5 70 V12" stroke="#4A4A4A" stroke-width="2"/>
                <path d="M25 14 Q44.5 -4 64 14 Q59 11 54.5 14 Q49.5 11 44.5 14 Q39.5 11 34.5 14 Q30 11 25 14 Z" fill="#6F7FA0" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                """),
    };

    /// <summary>Townsfolk who stroll across the village; they never speak.</summary>
    internal static readonly string[] Folk =
    [
        Person("#F1C27D", "#B5838D", "#6B4E3D", robe: true,
            hair: """
                <path d="M19 24 Q19 12 30 12 Q41 12 41 24 Q36 17 30 17 Q24 17 19 24 Z" fill="#7A4A2A" stroke="var(--v-ink)" stroke-width="1.5"/>
                <circle cx="30" cy="10" r="4.5" fill="#7A4A2A" stroke="var(--v-ink)" stroke-width="1.5"/>
                """,
            hand: """<path d="M39 66 H51 L49 75 H41 Z" fill="#C49A6C" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>"""),
        Person("#A86B3C", "#9C8F6E", "#5B5148",
            hair: """
                <path d="M19 20 Q20 11 30 11 Q40 11 41 20 Z" fill="#6B7F4E" stroke="var(--v-ink)" stroke-width="1.5"/>
                <path d="M39 19 H47" stroke="#6B7F4E" stroke-width="3" stroke-linecap="round"/>
                """),
    ];

    /// <summary>You: the king on his throne, crown, ermine collar and sceptre.</summary>
    internal const string King = """
        <svg viewBox="0 0 80 100" aria-hidden="true">
          <path d="M12 98 V20 Q12 6 40 4 Q68 6 68 20 V98 Z" fill="#8A5A36" stroke="var(--v-ink)" stroke-width="2.5" stroke-linejoin="round"/>
          <path d="M19 84 V24 Q19 13 40 11 Q61 13 61 24 V84 Z" fill="#B23A48" stroke="var(--v-ink)" stroke-width="2"/>
          <circle cx="40" cy="4" r="4" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="2"/>
          <rect x="14" y="82" width="52" height="16" rx="2" fill="#8A5A36" stroke="var(--v-ink)" stroke-width="2.5"/>
          <path d="M24 44 Q40 35 56 44 L60 88 H20 Z" fill="#7D2E46" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
          <path d="M23 45 Q40 56 57 45 Q40 39 23 45 Z" fill="#FFFDF6" stroke="var(--v-ink)" stroke-width="1.5"/>
          <g fill="var(--v-ink)"><circle cx="31" cy="47" r=".9"/><circle cx="40" cy="50" r=".9"/><circle cx="49" cy="47" r=".9"/></g>
          <path d="M30 88 h8 v7 h-10 z M42 88 h8 l2 7 h-10 z" fill="#3A2E28"/>
          <rect x="6" y="62" width="16" height="8" rx="3" fill="#8A5A36" stroke="var(--v-ink)" stroke-width="2"/>
          <rect x="58" y="62" width="16" height="8" rx="3" fill="#8A5A36" stroke="var(--v-ink)" stroke-width="2"/>
          <rect x="36.5" y="34" width="7" height="6" fill="#E8B48A"/>
          <circle cx="40" cy="27" r="11" fill="#E8B48A" stroke="var(--v-ink)" stroke-width="2"/>
          <path d="M30 30 Q40 44 50 30 Q45 35 40 35 Q35 35 30 30 Z" fill="#8A5A36" stroke="var(--v-ink)" stroke-width="1.5"/>
          <circle cx="36" cy="26" r="1.4" fill="var(--v-ink)"/><circle cx="44" cy="26" r="1.4" fill="var(--v-ink)"/>
          <path d="M29 19 V9 L34.5 14 L40 6 L45.5 14 L51 9 V19 Z" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
          <circle cx="40" cy="15" r="1.8" fill="#B23A48"/>
          <circle cx="21" cy="63" r="3.5" fill="#E8B48A" stroke="var(--v-ink)" stroke-width="1.5"/>
          <path d="M60 64 L65 34" stroke="#E2B13C" stroke-width="3" stroke-linecap="round"/>
          <circle cx="65.5" cy="31" r="3.5" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="1.5"/>
          <circle cx="59" cy="63" r="3.5" fill="#E8B48A" stroke="var(--v-ink)" stroke-width="1.5"/>
        </svg>
        """;

    internal const string Tree = """
        <svg viewBox="0 0 40 60" aria-hidden="true">
          <rect x="17" y="38" width="6" height="22" fill="#7A5634" stroke="var(--v-ink)" stroke-width="2"/>
          <circle cx="20" cy="22" r="15" fill="#5E9A4E" stroke="var(--v-ink)" stroke-width="2"/>
          <circle cx="11" cy="32" r="9" fill="#6FAA5C" stroke="var(--v-ink)" stroke-width="2"/>
          <circle cx="29" cy="32" r="9" fill="#6FAA5C" stroke="var(--v-ink)" stroke-width="2"/>
        </svg>
        """;

    /// <summary>
    /// One person facing right, feet at y=100 of a 60x100 box. <paramref name="hand"/> is drawn in the right hand
    /// (under the fist, so it looks held) and swings with that arm; <paramref name="front"/> goes over the torso.
    /// </summary>
    private static string Person(
        string skin, string cloth, string legs, string hair, string hand = "", string front = "", bool robe = false, bool frown = false) => $"""
        <svg viewBox="0 0 60 100" aria-hidden="true">
          <g class="leg leg-l"><rect x="22" y="66" width="7" height="28" rx="3" fill="{legs}" stroke="{Ink}" stroke-width="2"/><path d="M19 93 h11 v5 h-13 z" fill="#3A2E28"/></g>
          <g class="leg leg-r"><rect x="31" y="66" width="7" height="28" rx="3" fill="{legs}" stroke="{Ink}" stroke-width="2"/><path d="M30 93 h11 l3 5 h-14 z" fill="#3A2E28"/></g>
          <g class="arm arm-l"><rect x="12" y="42" width="7" height="24" rx="3.5" fill="{cloth}" stroke="{Ink}" stroke-width="2"/><circle cx="15.5" cy="67" r="3.5" fill="{skin}" stroke="{Ink}" stroke-width="1.5"/></g>
          <path d="M18 42 Q30 35 42 42 L{(robe ? "46 93 H14" : "44 72 H16")} Z" fill="{cloth}" stroke="{Ink}" stroke-width="2" stroke-linejoin="round"/>
          {front}
          <rect x="26.5" y="33" width="7" height="6" fill="{skin}"/>
          <circle cx="30" cy="25" r="11" fill="{skin}" stroke="{Ink}" stroke-width="2"/>
          <circle cx="29" cy="25" r="1.4" fill="{Ink}"/><circle cx="35.5" cy="25" r="1.4" fill="{Ink}"/>
          <path d="{(frown ? "M30 32 q3 -2.5 6 0" : "M30 30 q3 2.5 6 0")}" fill="none" stroke="{Ink}" stroke-width="1.5" stroke-linecap="round"/>
          {hair}
          <g class="arm arm-r">{hand}<rect x="41" y="42" width="7" height="24" rx="3.5" fill="{cloth}" stroke="{Ink}" stroke-width="2"/><circle cx="44.5" cy="67" r="3.5" fill="{skin}" stroke="{Ink}" stroke-width="1.5"/></g>
        </svg>
        """;
}
