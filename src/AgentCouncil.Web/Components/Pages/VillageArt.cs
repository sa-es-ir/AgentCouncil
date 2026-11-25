namespace AgentCouncil.Web.Components.Pages;

/// <summary>
/// The village's drawings as inline SVG: houses, people, the playground, the well and trees. All share the --v-ink outline.
/// People face right; CSS mirrors them, and swings the .leg-*/.arm-* groups to walk.
/// </summary>
internal static class VillageArt
{
    private const string Ink = "var(--v-ink)";

    // Three streets on a wide (21:9) scene seen from a little above: 3 houses at the back (Y 30), 4 in the middle (Y 62)
    // either side of the square, 3 at the front (Y 94). The playground takes the back right corner.
    // X is the door's centre and Y its threshold, in % of the scene; W is the width in %, smaller further back.
    // Every door sits at x=80 of its 160-wide drawing.
    internal static readonly (string Name, double X, double Y, double W, string Svg)[] Houses =
    [
        ("Baker", 25, 62, 10, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <rect x="30" y="64" width="100" height="76" rx="8" fill="#F7E3A1" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M16 72 Q80 4 144 72 Z" fill="var(--baker)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M42 58 Q80 24 118 58" fill="none" stroke="var(--v-ink)" stroke-width="2" opacity=".3"/>
              <ellipse cx="80" cy="54" rx="13" ry="6.5" fill="#D99A4E" stroke="var(--v-ink)" stroke-width="2.5"/>
              <path d="M73 51 l3 5 M79 50 l3 5 M85 51 l3 5" stroke="#9A5E22" stroke-width="1.6" stroke-linecap="round"/>
              <circle cx="52" cy="96" r="10" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <circle cx="108" cy="96" r="10" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M66 140 V114 a14 14 0 0 1 28 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M66 140 V114 a14 14 0 0 1 28 0 V140 Z" fill="#E88B3A" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M16 140 V118 M146 140 V122" stroke="var(--v-ink)" stroke-width="2"/>
              <circle cx="16" cy="114" r="6" fill="#F29BB5" stroke="var(--v-ink)" stroke-width="2"/>
              <circle cx="146" cy="118" r="6" fill="#F29BB5" stroke="var(--v-ink)" stroke-width="2"/>
            </svg>
            """),
        ("Merchant", 8, 62, 10, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <rect x="24" y="58" width="112" height="82" fill="#F2E2C4" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M14 62 L80 20 L146 62 Z" fill="#B5654A" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M18 74 H142 L134 92 H26 Z" fill="var(--merchant)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M45 75 L43 91 M70 75 L69 91 M95 75 L96 91 M120 75 L122 91" stroke="#FFFDF6" stroke-width="9" opacity=".8"/>
              <path d="M18 74 H142 L134 92 H26 Z" fill="none" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M136 50 H152 M148 50 V55" stroke="var(--v-ink)" stroke-width="2.5"/>
              <g class="coin"><circle cx="148" cy="62" r="7" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="2.5"/><path d="M148 58.5 V65.5" stroke="var(--v-ink)" stroke-width="1.5"/></g>
              <rect x="34" y="100" width="20" height="14" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M68 140 V106 H92 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M68 140 V106 H92 V140 Z" fill="#7A5A2E" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="30" y="120" width="20" height="20" fill="#C49A6C" stroke="var(--v-ink)" stroke-width="2.5"/>
              <path d="M30 120 L50 140 M50 120 L30 140" stroke="var(--v-ink)" stroke-width="1.5" opacity=".4"/>
              <rect x="108" y="124" width="22" height="16" fill="#C49A6C" stroke="var(--v-ink)" stroke-width="2.5"/>
              <g fill="#D9534F" stroke="var(--v-ink)" stroke-width="1.5"><circle cx="113" cy="121" r="4"/><circle cx="121" cy="120" r="4"/><circle cx="127" cy="122" r="3.5"/></g>
            </svg>
            """),
        ("Astronomer", 12, 30, 9, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <path d="M60 140 L63 46 L97 42 L100 140 Z" fill="#E1D9C9" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M52 50 L82 4 L108 44 Z" fill="var(--astronomer)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <g class="scope"><path d="M96 60 L132 42 L136 50 L100 68 Z" fill="#5E6E76" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <circle cx="134" cy="46" r="5" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="2"/></g>
              <path class="twinkle" d="M30 18 l1.6 4.4 4.4 1.6 -4.4 1.6 -1.6 4.4 -1.6 -4.4 -4.4 -1.6 4.4 -1.6 Z" fill="#FFD95A" stroke="var(--v-ink)" stroke-width="1.2"/>
              <rect x="73" y="60" width="14" height="18" rx="7" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <circle cx="80" cy="98" r="7" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M69 140 V122 a11 11 0 0 1 22 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M69 140 V122 a11 11 0 0 1 22 0 V140 Z" fill="#8E5A3C" stroke="var(--v-ink)" stroke-width="3"/>
            </svg>
            """),
        ("Bard", 20, 94, 11, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <rect x="26" y="66" width="108" height="74" fill="#F3E6C4" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M26 94 H134 M50 66 V140 M110 66 V140 M26 70 L50 94 M134 70 L110 94" stroke="#6E4F2C" stroke-width="3"/>
              <path d="M14 70 L80 26 L146 70 Z" fill="var(--bard)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <circle cx="80" cy="52" r="8" fill="#F7D774" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="32" y="104" width="13" height="14" fill="#F7D774" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="115" y="104" width="13" height="14" fill="#F7D774" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M68 140 V114 a12 12 0 0 1 24 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M68 140 V114 a12 12 0 0 1 24 0 V140 Z" fill="#8E5A3C" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M26 80 H8 M14 80 V86" stroke="var(--v-ink)" stroke-width="2.5"/>
              <rect x="4" y="86" width="20" height="17" rx="3" fill="#C98A3E" stroke="var(--v-ink)" stroke-width="2.5"/>
              <path d="M12 99 V91 L18 90 V97" fill="none" stroke="var(--v-ink)" stroke-width="1.8"/>
              <circle cx="11" cy="99" r="1.8" fill="var(--v-ink)"/><circle cx="17" cy="97.5" r="1.8" fill="var(--v-ink)"/>
              <g class="notes" fill="var(--v-ink)" stroke="var(--v-ink)" stroke-width="1.8">
                <g><circle cx="120" cy="44" r="2.6" stroke="none"/><path d="M122.5 44 V33" fill="none"/></g>
                <g><circle cx="134" cy="34" r="2.6" stroke="none"/><path d="M136.5 34 V23 l4 2" fill="none"/></g>
              </g>
            </svg>
            """),
        ("Mayor", 36, 30, 11, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <rect x="18" y="68" width="124" height="72" fill="#ECE8DD" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M80 24 V2" stroke="var(--v-ink)" stroke-width="3"/>
              <path class="flag" d="M81 3 L102 8 L81 14 Z" fill="var(--mayor)" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
              <path d="M8 70 L80 24 L152 70 Z" fill="var(--mayor)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
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
        ("Crier", 50, 94, 10, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <path d="M58 140 V54 H102 V140 Z" fill="#E1D9C9" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="54" y="30" width="52" height="26" fill="#D8D2C3" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M64 56 V42 a16 14 0 0 1 32 0 V56 Z" fill="#3A2E28"/>
              <g class="bell"><path d="M71 53 Q71 39 80 38 Q89 39 89 53 Z" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="2.5" stroke-linejoin="round"/><circle cx="80" cy="55" r="2.5" fill="#8A6B1A"/></g>
              <path d="M46 32 L80 4 L114 32 Z" fill="var(--crier)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <rect x="74" y="70" width="12" height="17" rx="6" fill="#D4EAF3" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M70 140 V118 a10 10 0 0 1 20 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M70 140 V118 a10 10 0 0 1 20 0 V140 Z" fill="#8E3B2A" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M118 140 V112 M146 140 V112" stroke="#7A5634" stroke-width="4"/>
              <rect x="112" y="96" width="40" height="22" fill="#C49A6C" stroke="var(--v-ink)" stroke-width="2.5"/>
              <rect x="117" y="100" width="12" height="14" fill="#FFFDF6" stroke="var(--v-ink)" stroke-width="1.5"/>
              <rect x="133" y="101" width="13" height="10" fill="#FFFDF6" stroke="var(--v-ink)" stroke-width="1.5"/>
            </svg>
            """),
        ("Seer", 92, 62, 10, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <path d="M80 26 V6" stroke="var(--v-ink)" stroke-width="3"/>
              <path class="flag" d="M81 7 L100 12 L81 17 Z" fill="#FFD95A" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
              <path d="M22 140 L80 26 L138 140 Z" fill="var(--seer)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M80 26 L56 140 M80 26 L104 140" stroke="#FFFDF6" stroke-width="3" opacity=".45"/>
              <path d="M75 58 a10 10 0 1 0 12 12 a8 8 0 1 1 -12 -12 Z" fill="#FFD95A" stroke="var(--v-ink)" stroke-width="2"/>
              <g class="twinkle" fill="#FFD95A" stroke="var(--v-ink)" stroke-width="1"><path d="M54 98 l1.5 4 4 1.5 -4 1.5 -1.5 4 -1.5 -4 -4 -1.5 4 -1.5 Z"/><path d="M108 92 l1.5 4 4 1.5 -4 1.5 -1.5 4 -1.5 -4 -4 -1.5 4 -1.5 Z"/></g>
              <path d="M64 140 L80 102 L96 140 Z" fill="#3A2E28"/>
              <path class="door" d="M64 140 L80 102 L96 140 Z" fill="#6B55A8" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <circle class="orb" cx="136" cy="122" r="7.5" fill="#CFE8FF" stroke="var(--v-ink)" stroke-width="2"/>
              <path d="M130 130 H142 L139 140 H133 Z" fill="#7A5634" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
            </svg>
            """),
        ("Blacksmith", 75, 62, 10, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <g class="smoke" fill="#DADFE2"><circle cx="118" cy="20" r="6"/><circle cx="118" cy="20" r="6"/><circle cx="118" cy="20" r="6"/></g>
              <rect x="110" y="26" width="16" height="40" fill="#8A6B55" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="12" y="76" width="136" height="64" fill="#D6B48E" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M4 80 L80 38 L156 80 Z" fill="var(--blacksmith)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <rect x="22" y="92" width="22" height="16" fill="#F2925B" stroke="var(--v-ink)" stroke-width="3"/>
              <rect x="56" y="96" width="48" height="44" fill="#3A2E28"/>
              <g class="door"><rect x="56" y="96" width="48" height="44" fill="#8A5A36" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M56 96 L104 140 M104 96 L56 140" stroke="var(--v-ink)" stroke-width="2" opacity=".45"/></g>
              <path d="M126 126 h24 l-5 5 h-4 v9 h-8 v-9 h-4 z" fill="#4A5258" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
            </svg>
            """),
        ("Elder", 60, 30, 9, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <rect x="32" y="72" width="96" height="68" rx="4" fill="#EADCC0" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M16 84 Q22 40 80 32 Q138 40 144 84 Q80 74 16 84 Z" fill="#C9A35A" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M36 62 Q80 50 124 62 M26 74 Q80 63 134 74" fill="none" stroke="var(--v-ink)" stroke-width="2" opacity=".3"/>
              <circle cx="52" cy="104" r="9" fill="#F7D774" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M52 95 V113 M43 104 H61" stroke="var(--v-ink)" stroke-width="2"/>
              <path d="M68 140 V116 a12 12 0 0 1 24 0 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M68 140 V116 a12 12 0 0 1 24 0 V140 Z" fill="var(--elder)" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M102 124 H128 M106 124 V134 M124 124 V134" stroke="#7A5634" stroke-width="4" stroke-linecap="round"/>
              <path d="M134 140 Q128 112 134 86" fill="none" stroke="#4E7F3E" stroke-width="3"/>
              <g fill="#5E9A4E" stroke="var(--v-ink)" stroke-width="1.5"><circle cx="130" cy="118" r="3.5"/><circle cx="136" cy="104" r="3.5"/><circle cx="131" cy="92" r="3"/></g>
            </svg>
            """),
        ("Gravedigger", 80, 94, 11, """
            <svg viewBox="0 0 160 140" aria-hidden="true">
              <path d="M46 140 L50 80 L114 76 L112 140 Z" fill="#C3BDD1" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <path d="M36 86 Q58 74 80 56 Q100 70 126 80 Z" fill="var(--gravedigger)" stroke="var(--v-ink)" stroke-width="3" stroke-linejoin="round"/>
              <rect x="94" y="96" width="12" height="12" transform="rotate(9 100 102)" fill="#AEB7C2" stroke="var(--v-ink)" stroke-width="3"/>
              <path d="M70 140 V118 H90 V140 Z" fill="#3A2E28"/>
              <path class="door" d="M70 140 V118 H90 V140 Z" fill="#6B6380" stroke="var(--v-ink)" stroke-width="3"/>
              <g class="rain" stroke="#6F8FB0" stroke-width="2.5" stroke-linecap="round">
                <path d="M62 52 l-2 6"/><path d="M74 52 l-2 6"/><path d="M86 52 l-2 6"/><path d="M98 52 l-2 6"/>
              </g>
              <path d="M52 34 a11 11 0 0 1 20 -8 a14 14 0 0 1 26 3 a10 10 0 0 1 2 19 H56 a9 9 0 0 1 -4 -14 Z" fill="#9AA1AD" stroke="var(--v-ink)" stroke-width="2.5" stroke-linejoin="round"/>
              <path d="M18 140 V127 a7 7 0 0 1 14 0 V140 Z" fill="#AEB7C2" stroke="var(--v-ink)" stroke-width="2.5"/>
              <path d="M25 129 V137 M21.5 132 H28.5" stroke="var(--v-ink)" stroke-width="1.5"/>
              <ellipse cx="134" cy="138" rx="14" ry="3" fill="#8FA6BF" opacity=".7"/>
            </svg>
            """),
    ];

    /// <summary>Each villager as a person: outfit and the thing in their hand say who they are.</summary>
    internal static readonly Dictionary<string, string> People = new()
    {
        // Top hat, chain of office and a rolled-up agenda.
        ["Mayor"] = Person("#E8B48A", "var(--mayor)", "#3F4A6B",
            hair: """
                <path d="M19 24 Q20 15 30 15 Q40 15 41 24 Q35 19 30 19 Q25 19 19 24 Z" fill="#9A9A9A" stroke="var(--v-ink)" stroke-width="1.5"/>
                <rect x="21" y="0" width="18" height="15" rx="1.5" fill="#2B2B33" stroke="var(--v-ink)" stroke-width="2"/>
                <rect x="21" y="9" width="18" height="3" fill="var(--mayor)"/>
                <rect x="15" y="13" width="30" height="4" rx="2" fill="#2B2B33" stroke="var(--v-ink)" stroke-width="2"/>
                """,
            front: """
                <path d="M21 43 Q30 55 39 43" fill="none" stroke="#E2B13C" stroke-width="2"/>
                <circle cx="30" cy="52" r="3" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="1.5"/>
                """,
            hand: """
                <rect x="40" y="60" width="9" height="17" rx="2" fill="#F3E6C4" stroke="var(--v-ink)" stroke-width="1.5"/>
                <path d="M40 72 H49" stroke="var(--v-ink)" stroke-width="1" opacity=".4"/>
                """),

        // Toque, apron and a fresh baguette.
        ["Baker"] = Person("#F1C27D", "#F3EFE6", "#6B5440",
            hair: """
                <path d="M19.5 27 Q19 21 21 19 M40.5 27 Q41 21 39 19" stroke="#B5651D" stroke-width="3" stroke-linecap="round"/>
                <path d="M20 17 Q13 9 20 5 Q24 -1 30 3 Q36 -1 40 5 Q47 9 40 17 Z" fill="#FFFFFF" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                <rect x="20" y="14" width="20" height="6" rx="1" fill="#FFFFFF" stroke="var(--v-ink)" stroke-width="2"/>
                """,
            front: """<path d="M22 50 H38 L40 72 H20 Z" fill="var(--baker)" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>""",
            hand: """
                <path d="M40 78 L53 50" stroke="var(--v-ink)" stroke-width="8" stroke-linecap="round"/>
                <path d="M40 78 L53 50" stroke="#D99A4E" stroke-width="5" stroke-linecap="round"/>
                <path d="M46.5 64 l3 -.5 M49 58.5 l3 -.5 M51.3 53.5 l2.5 -.5" stroke="#9A5E22" stroke-width="1.2"/>
                """),

        // Starry pointed hat, spectacles and a spyglass.
        ["Astronomer"] = Person("#C68642", "var(--astronomer)", "#2F3A5A", robe: true,
            hair: """
                <path d="M19.5 21 Q19 27 20.5 31 M40.5 21 Q41 27 39.5 31" stroke="#BDBDBD" stroke-width="2.5" stroke-linecap="round"/>
                <circle cx="29" cy="25" r="3.6" fill="#D4EAF3" fill-opacity=".45" stroke="var(--v-ink)" stroke-width="1.5"/>
                <circle cx="35.5" cy="25" r="3.6" fill="#D4EAF3" fill-opacity=".45" stroke="var(--v-ink)" stroke-width="1.5"/>
                <path d="M15 19 L31 -5 L45 19 Q30 15 15 19 Z" fill="#2F3A6B" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                <path d="M31 4 l1.4 3.6 3.6 1.4 -3.6 1.4 -1.4 3.6 -1.4 -3.6 -3.6 -1.4 3.6 -1.4 Z" fill="#FFD95A"/>
                <circle cx="24" cy="15" r="1" fill="#FFD95A"/><circle cx="37" cy="13" r=".9" fill="#FFD95A"/>
                """,
            hand: """
                <path d="M41 70 L46 72.5 L57 51 L52 48.5 Z" fill="#B08D57" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>
                <path d="M51.5 48.5 L57.5 51.5 L59.5 47.5 L53.5 44.5 Z" fill="#7A5634" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>
                """),

        // Leather apron and a hammer.
        ["Blacksmith"] = Person("#8D5524", "#7A6250", "#4A3B30",
            hair: """<path d="M19 22 Q20 13 30 13 Q40 13 41 22 Q35 17 30 17 Q25 17 19 22 Z" fill="#2E2018" stroke="var(--v-ink)" stroke-width="1.5"/>""",
            front: """<path d="M21 48 H39 L41 74 H19 Z" fill="var(--blacksmith)" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>""",
            hand: """
                <path d="M44.5 70 V50" stroke="#7A5634" stroke-width="3" stroke-linecap="round"/>
                <rect x="38" y="44" width="13" height="7" rx="1.5" fill="#5A6268" stroke="var(--v-ink)" stroke-width="1.5"/>
                """),

        // Hooded, frowning, leaning on a shovel.
        ["Gravedigger"] = Person("#E0AC69", "var(--gravedigger)", "#4B4560", robe: true, frown: true,
            hair: """<path d="M17 31 Q15 9 30 9 Q45 9 43 31 Q42 18 30 17 Q18 18 17 31 Z" fill="var(--gravedigger)" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>""",
            hand: """
                <path d="M44.5 84 V12 M40 12 H49" stroke="#7A5634" stroke-width="3" stroke-linecap="round"/>
                <path d="M39 83 H50 V92 Q44.5 100 39 92 Z" fill="#8A949C" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                """),

        // Flat cap, moustache, waistcoat and a purse of coin.
        ["Merchant"] = Person("#E0AC69", "var(--merchant)", "#4A3B30",
            hair: """
                <path d="M19 24 Q19 14 30 14 Q41 14 41 24 Q35 19 30 19 Q25 19 19 24 Z" fill="#3B2A1E" stroke="var(--v-ink)" stroke-width="1.5"/>
                <path d="M18 19 Q19 9 31 9 Q42 9 42 17 Z" fill="#7A5A2E" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                <path d="M40 16 Q46 16 48 19.5 H38 Z" fill="#7A5A2E" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>
                <path d="M30 29 q3 -2 6.5 0 q-3.2 2 -6.5 0 Z" fill="#3B2A1E"/>
                """,
            front: """
                <path d="M21 46 L30 58 L39 46 L41 72 H19 Z" fill="#C9A13B" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>
                <circle cx="30" cy="63" r="1" fill="var(--v-ink)"/><circle cx="30" cy="68" r="1" fill="var(--v-ink)"/>
                """,
            hand: """
                <path d="M44.5 69 V74" stroke="#7A5634" stroke-width="1.5"/>
                <path d="M39 77 Q44.5 72 50 77 Q53 87 44.5 88 Q36 87 39 77 Z" fill="#9C6B3C" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>
                <circle cx="44.5" cy="82" r="2.5" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="1"/>
                """),

        // White hair and beard, long robe, a walking staff with a green stone.
        ["Elder"] = Person("#E8B48A", "var(--elder)", "#5A4630", robe: true,
            hair: """
                <path d="M19 24 Q19 12 30 12 Q41 12 41 24 Q38 17 30 17 Q22 17 19 24 Z" fill="#EDEDED" stroke="var(--v-ink)" stroke-width="2"/>
                <path d="M21 28 Q30 46 40 28 Q36 34 30 34 Q24 34 21 28 Z" fill="#EDEDED" stroke="var(--v-ink)" stroke-width="1.5"/>
                """,
            hand: """
                <path d="M44.5 26 V98" stroke="#7A5634" stroke-width="3" stroke-linecap="round"/>
                <circle cx="44.5" cy="23" r="4.5" fill="#7FC8A9" stroke="var(--v-ink)" stroke-width="2"/>
                """),

        // Starry hood, a gold pendant and a crystal ball.
        ["Seer"] = Person("#A86B3C", "var(--seer)", "#3E3355", robe: true,
            hair: """
                <path d="M17 31 Q15 9 30 9 Q45 9 43 31 Q42 18 30 17 Q18 18 17 31 Z" fill="var(--seer)" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                <circle cx="22" cy="19" r="1.1" fill="#FFD95A"/><circle cx="35" cy="12.5" r="1.1" fill="#FFD95A"/><circle cx="40" cy="22" r=".9" fill="#FFD95A"/>
                """,
            front: """<circle cx="30" cy="49" r="2.4" fill="#FFD95A" stroke="var(--v-ink)" stroke-width="1"/>""",
            hand: """
                <circle class="orb" cx="49" cy="75" r="6.5" fill="#CFE8FF" fill-opacity=".9" stroke="var(--v-ink)" stroke-width="2"/>
                <path d="M46 72.5 q2 -2.3 4.6 -1.8" fill="none" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round"/>
                """),

        // Feathered beret and a lute.
        ["Bard"] = Person("#F1C27D", "var(--bard)", "#5B3A4A",
            hair: """
                <path d="M19 24 Q20 15 30 15 Q40 15 41 24 Q35 19 28 20 Z" fill="#C9772B" stroke="var(--v-ink)" stroke-width="1.5"/>
                <path d="M20 14 Q10 1 3 4 Q11 8 20 16 Z" fill="#F7E3A1" stroke="var(--v-ink)" stroke-width="1.5" stroke-linejoin="round"/>
                <ellipse cx="29" cy="15" rx="13" ry="5" fill="var(--bard)" stroke="var(--v-ink)" stroke-width="2"/>
                """,
            hand: """
                <path d="M44.5 70 V54 M42 54 H47" stroke="#7A5634" stroke-width="3" stroke-linecap="round"/>
                <ellipse cx="44.5" cy="79" rx="7" ry="9" fill="#C98A3E" stroke="var(--v-ink)" stroke-width="2"/>
                <circle cx="44.5" cy="79" r="2.2" fill="#3A2E28"/>
                """),

        // Tricorn, belted coat and a handbell.
        ["Crier"] = Person("#E8B48A", "var(--crier)", "#2E3A4A",
            hair: """
                <path d="M19.5 22 Q18.5 27 19.5 30" stroke="#EDEDED" stroke-width="3" stroke-linecap="round"/>
                <path d="M15 19 L22 8 Q30 4 38 8 L45 19 Q30 14 15 19 Z" fill="#2B2B33" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                <path d="M17 18 Q30 13 43 18" fill="none" stroke="#E2B13C" stroke-width="1.5"/>
                """,
            front: """
                <path d="M18 62 H42" stroke="#3A2E28" stroke-width="3"/>
                <rect x="28" y="60" width="4" height="4" fill="#E2B13C"/>
                """,
            hand: """
                <path d="M44.5 69 V75" stroke="#7A5634" stroke-width="3"/>
                <path d="M38 87 Q38 76 44.5 75 Q51 76 51 87 Z" fill="#E2B13C" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
                <circle cx="44.5" cy="89" r="1.8" fill="#8A6B1A"/>
                """),
    };

    /// <summary>The well in the middle of the square, where the news is told.</summary>
    internal const string Well = """
        <svg viewBox="0 0 60 64" aria-hidden="true">
          <path d="M14 23 V46 M46 23 V46" stroke="#7A5634" stroke-width="3"/>
          <path d="M8 24 L30 8 L52 24 Z" fill="#B5654A" stroke="var(--v-ink)" stroke-width="2.5" stroke-linejoin="round"/>
          <path d="M16 31 H44" stroke="#7A5634" stroke-width="2.5" stroke-linecap="round"/>
          <path d="M30 31 V39" stroke="var(--v-ink)" stroke-width="1.2"/>
          <rect x="26.5" y="38" width="7" height="6" rx="1" fill="#8A6B55" stroke="var(--v-ink)" stroke-width="1.2"/>
          <ellipse cx="30" cy="47" rx="20" ry="6" fill="#3A4A55" stroke="var(--v-ink)" stroke-width="2.5"/>
          <path d="M10 47 V56 Q30 64 50 56 V47 Q30 55 10 47 Z" fill="#B8B2A6" stroke="var(--v-ink)" stroke-width="2.5" stroke-linejoin="round"/>
          <path d="M19 51 V59 M30 53 V62 M41 51 V59" stroke="var(--v-ink)" stroke-width="1.2" opacity=".35"/>
        </svg>
        """;

    /// <summary>The children on the playground: they only play, never talk. Same build as the grown-ups, drawn smaller.</summary>
    internal static readonly string[] Kids =
    [
        // Pigtails.
        Person("#F1C27D", "#E8694A", "#3F4A6B",
            hair: """
                <path d="M19 24 Q20 13 30 13 Q40 13 41 24 Q35 18 30 18 Q25 18 19 24 Z" fill="#B5651D" stroke="var(--v-ink)" stroke-width="1.5"/>
                <circle cx="17" cy="27" r="4" fill="#B5651D" stroke="var(--v-ink)" stroke-width="1.5"/>
                <circle cx="43" cy="27" r="4" fill="#B5651D" stroke="var(--v-ink)" stroke-width="1.5"/>
                """),

        // Cap on backwards.
        Person("#8D5A3B", "#5AA9C9", "#6B5440",
            hair: """
                <path d="M19 22 Q20 12 30 12 Q40 12 41 22 Z" fill="#E8C547" stroke="var(--v-ink)" stroke-width="2"/>
                <rect x="12" y="19" width="12" height="3.5" rx="1.5" fill="#E8C547" stroke="var(--v-ink)" stroke-width="1.5"/>
                """),

        // Messy hair, chasing the dog.
        Person("#E8B48A", "#3FBF7F", "#3F4A6B",
            hair: """
                <path d="M19 24 Q18 14 24 15 Q27 10 32 13 Q38 10 40 16 Q43 19 41 24 Q36 18 30 19 Q24 18 19 24 Z" fill="#2B2B33" stroke="var(--v-ink)" stroke-width="1.5"/>
                """),
    ];

    /// <summary>A swing frame with a child on it; the .swing-seat group swings from the top bar.</summary>
    internal const string Swing = """
        <svg viewBox="0 0 80 90" aria-hidden="true">
          <path d="M8 90 L18 6 L28 90 M52 90 L62 6 L72 90" fill="none" stroke="var(--v-ink)" stroke-width="6" stroke-linecap="round"/>
          <path d="M8 90 L18 6 L28 90 M52 90 L62 6 L72 90" fill="none" stroke="#9A6B42" stroke-width="3" stroke-linecap="round"/>
          <path d="M14 7 H66" stroke="var(--v-ink)" stroke-width="7" stroke-linecap="round"/>
          <path d="M14 7 H66" stroke="#9A6B42" stroke-width="4" stroke-linecap="round"/>
          <g class="swing-seat">
            <path d="M32 7 V62 M48 7 V62" stroke="var(--v-ink)" stroke-width="1.5"/>
            <path d="M38 61 L52 70 M42 61 L55 66" stroke="var(--v-ink)" stroke-width="7" stroke-linecap="round"/>
            <path d="M38 61 L52 70 M42 61 L55 66" stroke="#3F4A6B" stroke-width="4" stroke-linecap="round"/>
            <path d="M33 46 Q40 41 47 46 L46 61 H34 Z" fill="#A08BD6" stroke="var(--v-ink)" stroke-width="2" stroke-linejoin="round"/>
            <rect x="27" y="61" width="26" height="4" rx="1.5" fill="#9A6B42" stroke="var(--v-ink)" stroke-width="1.5"/>
            <circle cx="40" cy="37" r="8" fill="#F1C27D" stroke="var(--v-ink)" stroke-width="2"/>
            <path d="M32 36 Q33 28 40 28 Q47 28 48 36 Q44 32 40 32 Q36 32 32 36 Z" fill="#E8C547" stroke="var(--v-ink)" stroke-width="1.2"/>
            <circle cx="39" cy="37" r="1.1" fill="var(--v-ink)"/><circle cx="44" cy="37" r="1.1" fill="var(--v-ink)"/>
            <path d="M40 41 q2.5 2 5 0" fill="none" stroke="var(--v-ink)" stroke-width="1.2" stroke-linecap="round"/>
          </g>
        </svg>
        """;

    /// <summary>The dog the third child chases, facing right.</summary>
    internal const string Dog = """
        <svg viewBox="0 0 44 30" aria-hidden="true">
          <g class="tail"><path d="M9 14 Q3 9 5 3" fill="none" stroke="var(--v-ink)" stroke-width="4.5" stroke-linecap="round"/><path d="M9 14 Q3 9 5 3" fill="none" stroke="#C8955A" stroke-width="2.5" stroke-linecap="round"/></g>
          <g class="leg leg-l"><rect x="11" y="19" width="4.5" height="10" rx="2" fill="#C8955A" stroke="var(--v-ink)" stroke-width="1.5"/></g>
          <g class="leg leg-r"><rect x="25" y="19" width="4.5" height="10" rx="2" fill="#C8955A" stroke="var(--v-ink)" stroke-width="1.5"/></g>
          <ellipse cx="20" cy="16" rx="12" ry="6.5" fill="#C8955A" stroke="var(--v-ink)" stroke-width="2"/>
          <circle cx="34" cy="11" r="6.5" fill="#C8955A" stroke="var(--v-ink)" stroke-width="2"/>
          <path d="M31 6 Q27 12 31 15" fill="#8A5A30" stroke="var(--v-ink)" stroke-width="1.5"/>
          <circle cx="36.5" cy="9.5" r="1" fill="var(--v-ink)"/><circle cx="40.5" cy="12" r="1.6" fill="var(--v-ink)"/>
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
